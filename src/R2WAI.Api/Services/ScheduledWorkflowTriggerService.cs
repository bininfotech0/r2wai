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
/// Replica-safe: each tick runs under a Postgres session advisory lock, so only one API replica
/// evaluates schedules at a time. Each scheduled run records its occurrence in the instance input
/// data, and a per-workflow cursor catches up occurrences missed during downtime.
/// </summary>
public sealed class ScheduledWorkflowTriggerService(
    IServiceProvider serviceProvider, ILogger<ScheduledWorkflowTriggerService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int MaxOccurrencesPerWorkflowPerSweep = 100;

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
                .Select(w => new
                {
                    w.Id,
                    w.TenantId,
                    w.UserId,
                    w.Name,
                    w.Trigger,
                    Since = w.ModifiedAt ?? w.CreatedAt,
                    w.LastScheduleOccurrenceAt
                })
                .ToListAsync(ct);

            var started = 0;
            foreach (var workflow in candidates)
            {
                // A saved schedule cursor catches up missed runs after an outage. The modified time
                // remains a lower bound so editing/publishing a schedule never fires old slots.
                var from = workflow.LastScheduleOccurrenceAt is { } cursor && cursor > workflow.Since
                    ? cursor
                    : workflow.Since;
                var occurrences = WorkflowTriggerSpec.Parse(workflow.Trigger)
                    .OccurrencesBetween(from, nowUtc)
                    .Take(MaxOccurrencesPerWorkflowPerSweep + 1)
                    .ToList();

                var processedAll = occurrences.Count <= MaxOccurrencesPerWorkflowPerSweep;
                DateTime? lastProcessed = workflow.LastScheduleOccurrenceAt;
                foreach (var occurrence in occurrences.Take(MaxOccurrencesPerWorkflowPerSweep))
                {
                    var scheduledFor = occurrence.ToString("O");
                    if (await AlreadyStartedAsync(dbContext, workflow.Id, scheduledFor, isNpgsql, ct))
                    {
                        lastProcessed = occurrence;
                        continue;
                    }

                    if (await StartAsync(workflow.Id, workflow.TenantId, workflow.UserId, workflow.Name, scheduledFor, ct))
                    {
                        started++;
                        lastProcessed = occurrence;
                    }
                    else
                    {
                        // Retry this occurrence on the next poll instead of silently skipping it.
                        processedAll = false;
                        break;
                    }
                }

                if (lastProcessed is { } lastOccurrence
                    && (workflow.LastScheduleOccurrenceAt is null || lastOccurrence > workflow.LastScheduleOccurrenceAt))
                    await AdvanceScheduleCursorAsync(dbContext, workflow.Id, lastOccurrence, ct);

                if (processedAll)
                    await AdvanceScheduleCursorAsync(dbContext, workflow.Id, nowUtc, ct);
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

    private static async Task AdvanceScheduleCursorAsync(
        ApplicationDbContext dbContext, Guid workflowId, DateTime cursorUtc, CancellationToken ct)
    {
        var query = dbContext.Workflows.IgnoreQueryFilters()
            .Where(w => w.Id == workflowId
                && (w.LastScheduleOccurrenceAt == null || w.LastScheduleOccurrenceAt < cursorUtc));

        if (dbContext.Database.IsRelational())
        {
            await query.ExecuteUpdateAsync(
                updates => updates.SetProperty(w => w.LastScheduleOccurrenceAt, cursorUtc), ct);
            return;
        }

        var workflow = await query.FirstOrDefaultAsync(ct);
        if (workflow is null)
            return;

        workflow.AdvanceScheduleOccurrence(cursorUtc);
        await dbContext.SaveChangesAsync(ct);
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
