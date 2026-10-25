using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Services;

/// <summary>
/// Resumes due workflow Delay steps. Work is claimed in bounded batches and each claim has an
/// owner id plus a renewable lease, so long-running continuations cannot be reclaimed by another
/// API replica while they are still executing.
/// </summary>
public sealed class WorkflowDelayResumeBackgroundService(
    IServiceProvider serviceProvider, ILogger<WorkflowDelayResumeBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromMinutes(2);
    private const int MaxInstancesPerSweep = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Workflow delay-resume background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, stoppingToken);
                await ResumeDueDelaysAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Workflow delay-resume sweep failed");
            }
        }

        logger.LogInformation("Workflow delay-resume background service stopped");
    }

    private async Task ResumeDueDelaysAsync(CancellationToken ct)
    {
        List<Guid> dueInstanceIds;
        using (var queryScope = serviceProvider.CreateScope())
        {
            var dbContext = queryScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            dueInstanceIds = await dbContext.WorkflowInstances
                .IgnoreQueryFilters()
                .Where(i => !i.IsDeleted && i.PendingResumeAt != null && i.PendingResumeAt <= now
                    && (i.PendingResumeLeaseExpiresAt == null || i.PendingResumeLeaseExpiresAt <= now))
                .OrderBy(i => i.PendingResumeAt)
                .ThenBy(i => i.Id)
                .Select(i => i.Id)
                .Take(MaxInstancesPerSweep)
                .ToListAsync(ct);
        }

        foreach (var instanceId in dueInstanceIds)
        {
            ct.ThrowIfCancellationRequested();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            var leaseId = Guid.NewGuid();
            if (!await TryClaimAsync(dbContext, instanceId, leaseId, now, ct))
                continue;

            var workflowBridge = scope.ServiceProvider.GetRequiredService<IWorkflowBridge>();
            using var runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var heartbeat = RenewLeaseWhileRunningAsync(instanceId, leaseId, runCts, runCts.Token);

            try
            {
                await workflowBridge.ContinueDelayedWorkflowAsync(instanceId, runCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (runCts.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Stopped delayed workflow continuation {InstanceId} after its lease was lost",
                    instanceId);
            }
            catch (Exception ex)
            {
                // Keep the lease until expiry after a failed continuation. That gives in-flight
                // external actions time to settle before another replica attempts recovery.
                logger.LogError(ex, "Failed to continue delayed R2WAI instance {InstanceId}", instanceId);
            }
            finally
            {
                runCts.Cancel();
                try
                {
                    await heartbeat;
                }
                catch (OperationCanceledException)
                {
                    // Expected when the continuation finishes or the host is stopping.
                }
            }
        }
    }

    private static async Task<bool> TryClaimAsync(
        ApplicationDbContext dbContext, Guid instanceId, Guid leaseId, DateTime now, CancellationToken ct)
    {
        var leaseExpiresAt = now.Add(LeaseDuration);
        var query = dbContext.WorkflowInstances
            .IgnoreQueryFilters()
            .Where(i => i.Id == instanceId && !i.IsDeleted && i.PendingResumeAt != null && i.PendingResumeAt <= now
                && (i.PendingResumeLeaseExpiresAt == null || i.PendingResumeLeaseExpiresAt <= now));

        if (dbContext.Database.IsRelational())
        {
            var claimed = await query.ExecuteUpdateAsync(s => s
                .SetProperty(i => i.PendingResumeLeaseExpiresAt, leaseExpiresAt)
                .SetProperty(i => i.PendingResumeLeaseId, leaseId), ct);
            return claimed == 1;
        }

        // EF InMemory doesn't implement ExecuteUpdateAsync; keep its single-process path usable.
        var instance = await query.FirstOrDefaultAsync(ct);
        if (instance is null)
            return false;

        instance.SetPendingResumeLease(leaseId, leaseExpiresAt);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    private async Task RenewLeaseWhileRunningAsync(
        Guid instanceId, Guid leaseId, CancellationTokenSource runCts, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LeaseRenewalInterval, ct);
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var expiresAt = DateTime.UtcNow.Add(LeaseDuration);
                var query = dbContext.WorkflowInstances
                    .IgnoreQueryFilters()
                    .Where(i => i.Id == instanceId && !i.IsDeleted
                        && i.PendingResumeAt != null && i.PendingResumeLeaseId == leaseId);
                var renewed = 0;
                if (dbContext.Database.IsRelational())
                {
                    renewed = await query.ExecuteUpdateAsync(
                        s => s.SetProperty(i => i.PendingResumeLeaseExpiresAt, expiresAt), ct);
                }
                else
                {
                    var instance = await query.FirstOrDefaultAsync(ct);
                    if (instance is not null)
                    {
                        instance.SetPendingResumeLease(leaseId, expiresAt);
                        renewed = await dbContext.SaveChangesAsync(ct);
                    }
                }

                if (renewed > 0)
                    continue;

                var current = await dbContext.WorkflowInstances
                    .IgnoreQueryFilters()
                    .Where(i => i.Id == instanceId)
                    .Select(i => new { i.PendingResumeAt, i.PendingResumeLeaseId })
                    .FirstOrDefaultAsync(ct);

                // A completed continuation clears both fields. Any other mismatch means another
                // replica owns the claim, so cancel this execution before it can run more steps.
                if (current is null || current.PendingResumeLeaseId is null)
                    return;

                logger.LogWarning("Lost lease for delayed workflow instance {InstanceId}", instanceId);
                runCts.Cancel();
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not renew delayed workflow lease for instance {InstanceId}", instanceId);
                runCts.Cancel();
                return;
            }
        }
    }
}
