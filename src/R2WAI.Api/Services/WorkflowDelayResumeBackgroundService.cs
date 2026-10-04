using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Services;

/// <summary>
/// Polls for WorkflowInstance rows whose Delay step has elapsed (WorkflowInstance.PendingResumeAt)
/// and continues their execution — the R2WAI-owned replacement for Elsa's native Delay+scheduler
/// resume, which does not work reliably for these dynamically-built Flowchart definitions (see
/// WorkflowBridge.ResumeWorkflowAsync's doc comment for the full root cause).
///
/// The claim grants a time-boxed lease rather than clearing PendingResumeAt outright
/// (implementation plan Phase 5): a replica that claimed a due instance and then crashed before
/// ContinueDelayedWorkflowAsync finished used to leave that instance permanently stuck —
/// PendingResumeAt cleared, PendingResumeStepIndex still set, Status still Running throughout, so
/// nothing distinguished it from a normal in-progress instance. Now a stale lease
/// (PendingResumeLeaseExpiresAt elapsed, PendingResumeAt still set) is picked back up by the next
/// sweep just like a never-claimed due instance.
/// </summary>
public sealed class WorkflowDelayResumeBackgroundService(
    IServiceProvider serviceProvider, ILogger<WorkflowDelayResumeBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    // Generous relative to PollInterval: ContinueDelayedWorkflowAsync can run an arbitrary number of
    // remaining workflow steps (API calls, AI generation, emails), not just one quick action.
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);

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
            catch (OperationCanceledException)
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
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var workflowBridge = scope.ServiceProvider.GetRequiredService<IWorkflowBridge>();

        var now = DateTime.UtcNow;
        // IgnoreQueryFilters: a cross-tenant background sweep — no HttpContext here means the tenant
        // filter would otherwise accidentally match every tenant's rows instead of being explicitly
        // correct. Matches the pattern established in ApprovalService.EscalateOverdueAsync.
        var dueInstanceIds = await dbContext.WorkflowInstances
            .IgnoreQueryFilters()
            .Where(i => !i.IsDeleted && i.PendingResumeAt != null && i.PendingResumeAt <= now
                && (i.PendingResumeLeaseExpiresAt == null || i.PendingResumeLeaseExpiresAt <= now))
            .Select(i => i.Id)
            .ToListAsync(ct);

        // ExecuteUpdateAsync (the atomic claim below) is a relational-only feature — the EF Core
        // InMemory provider used by the fast API test suite doesn't support it at all and throws.
        // Real deployments are always relational (Postgres), so this only ever takes the
        // non-atomic fallback path under tests, never in production.
        var canClaimAtomically = dbContext.Database.IsRelational();

        foreach (var instanceId in dueInstanceIds)
        {
            // Atomic claim before doing any work: a conditional UPDATE that only succeeds (affects a
            // row) if PendingResumeAt is still set/due and no other replica currently holds an
            // unexpired lease on it, same affected-row-count idiom BackgroundJobProcessor uses.
            // Without this, two API replicas polling the same due instance in the same window would
            // both call ContinueDelayedWorkflowAsync concurrently and run the same remaining
            // workflow steps twice — this was P0-3 from the 2026-09-20 audit.
            //
            // Grants a time-boxed lease rather than clearing PendingResumeAt (implementation plan
            // Phase 5) — clearing it here used to be the claim signal, but left no way to tell a
            // genuinely-abandoned claim (replica crashed mid-resume) from a real completion, so a
            // crash here silently stranded the instance forever. ContinueDelayedWorkflowAsync still
            // reads PendingResumeStepIndex to know which step to resume from, and calls
            // ClearPendingResume() at the end, which now also clears this lease.
            if (canClaimAtomically)
            {
                var leaseExpiresAt = DateTime.UtcNow.Add(LeaseDuration);
                var claimed = await dbContext.WorkflowInstances
                    .IgnoreQueryFilters()
                    .Where(i => i.Id == instanceId && !i.IsDeleted && i.PendingResumeAt != null && i.PendingResumeAt <= now
                        && (i.PendingResumeLeaseExpiresAt == null || i.PendingResumeLeaseExpiresAt <= now))
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.PendingResumeLeaseExpiresAt, leaseExpiresAt), ct);

                if (claimed == 0)
                    continue; // another replica claimed it first, or already holds a live lease
            }

            try
            {
                await workflowBridge.ContinueDelayedWorkflowAsync(instanceId, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to continue delayed R2WAI instance {InstanceId}", instanceId);
            }
        }
    }
}
