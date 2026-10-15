using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Workflows;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Services;

/// <summary>
/// Starts published automations whose trigger is a schedule (<c>schedule:&lt;cron&gt;|&lt;tz&gt;</c>, see
/// <see cref="WorkflowTriggerSpec"/>). Before this existed the wizard offered "Schedule" but nothing
/// ever fired it.
///
/// Replica-safe without new schema: each tick runs under a Postgres session advisory lock, so only one
/// API replica evaluates schedules at a time, and every scheduled run records its occurrence in the
/// instance's input data (<c>{"trigger":"schedule","scheduledFor":"…"}</c>), which is what dedupes an
/// occurrence across ticks, replicas and restarts. Misfire policy is "skip": an occurrence older than
/// <see cref="Lookback"/> (API down for longer than that) is not caught up.
/// </summary>
public sealed class ScheduledWorkflowTriggerService(
    IServiceProvider serviceProvider, ILogger<ScheduledWorkflowTriggerService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Lookback = TimeSpan.FromMinutes(5);

    // Arbitrary but fixed: identifies this sweeper's advisory lock across replicas.
    private const long AdvisoryLockKey = 7_204_215_001;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scheduled automation trigger service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, stoppingToken);
                await RunDueSchedulesAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled automation sweep failed");
            }
        }

        logger.LogInformation("Scheduled automation trigger service stopped");
    }

    public async Task<int> RunDueSchedulesAsync(DateTime nowUtc, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var isNpgsql = dbContext.Database.IsNpgsql();

        if (isNpgsql)
        {
            await dbContext.Database.OpenConnectionAsync(ct);
            if (!await TryAdvisoryLockAsync(dbContext, "pg_try_advisory_lock", ct))
            {
                await dbContext.Database.CloseConnectionAsync();
                return 0; // another replica is running this tick
            }
        }

        try
        {
            // IgnoreQueryFilters: cross-tenant background sweep with no signed-in user — the fail-closed
            // tenant filter would match nothing. Tenant comes from each row and is passed explicitly on.
            var candidates = await dbContext.Workflows
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => !w.IsDeleted && w.IsActive && w.VersionStatus == "Published"
                    && w.Trigger != null && w.Trigger.StartsWith(WorkflowTriggerSpec.SchedulePrefix))
                .Select(w => new { w.Id, w.TenantId, w.UserId, w.Name, w.Trigger, Since = w.ModifiedAt ?? w.CreatedAt })
                .ToListAsync(ct);

            var started = 0;
            foreach (var workflow in candidates)
            {
                // Never fire an occurrence from before the schedule (or anything else) was last saved —
                // otherwise publishing at 09:02 would immediately run the 09:00 slot.
                var from = workflow.Since > nowUtc - Lookback ? workflow.Since : nowUtc - Lookback;
                foreach (var occurrence in WorkflowTriggerSpec.Parse(workflow.Trigger).OccurrencesBetween(from, nowUtc))
                {
                    var scheduledFor = occurrence.ToString("O");
                    if (await AlreadyStartedAsync(dbContext, workflow.Id, scheduledFor, isNpgsql, ct))
                        continue;

                    if (await StartAsync(workflow.Id, workflow.TenantId, workflow.UserId, workflow.Name, scheduledFor, ct))
                        started++;
                }
            }

            return started;
        }
        finally
        {
            if (isNpgsql)
            {
                await TryAdvisoryLockAsync(dbContext, "pg_advisory_unlock", CancellationToken.None);
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }

    private static async Task<bool> AlreadyStartedAsync(
        ApplicationDbContext dbContext, Guid workflowId, string scheduledFor, bool isNpgsql, CancellationToken ct)
    {
        var instances = dbContext.WorkflowInstances.IgnoreQueryFilters()
            .Where(i => i.WorkflowId == workflowId && i.Data != null);

        if (isNpgsql)
        {
            var marker = JsonSerializer.Serialize(new { scheduledFor });
            return await instances.AnyAsync(i => EF.Functions.JsonContains(i.Data!, marker), ct);
        }

        return await instances.AnyAsync(i => i.Data!.Contains(scheduledFor), ct);
    }

    private async Task<bool> StartAsync(Guid workflowId, Guid tenantId, Guid userId, string name, string scheduledFor, CancellationToken ct)
    {
        // A fresh scope per run: one failing run must not leave tracked state behind for the next.
        using var scope = serviceProvider.CreateScope();
        var bridge = scope.ServiceProvider.GetRequiredService<IWorkflowBridge>();
        var data = JsonSerializer.Serialize(new { trigger = "schedule", scheduledFor });

        try
        {
            var (_, instanceId) = await bridge.StartWorkflowAsync(workflowId, tenantId, userId, data, ct);
            logger.LogInformation("Schedule started workflow {WorkflowId} for {ScheduledFor} → instance {InstanceId}",
                workflowId, scheduledFor, instanceId);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Scheduled run of workflow {WorkflowId} for {ScheduledFor} failed to start", workflowId, scheduledFor);
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifications.SendAsync(userId.ToString(), "Scheduled automation failed to start",
                $"\"{name}\" could not start its run scheduled for {scheduledFor}.", "error", $"/automations/{workflowId}", CancellationToken.None);
            return false;
        }
    }

    private static async Task<bool> TryAdvisoryLockAsync(ApplicationDbContext dbContext, string function, CancellationToken ct)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {function}({AdvisoryLockKey})";
        return await command.ExecuteScalarAsync(ct) is true;
    }
}
