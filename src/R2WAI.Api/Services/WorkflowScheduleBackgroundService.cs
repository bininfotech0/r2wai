using Cronos;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Services;

public sealed class WorkflowScheduleBackgroundService(
    IServiceProvider serviceProvider, ILogger<WorkflowScheduleBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Workflow schedule background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, stoppingToken);
                await RunDueSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Workflow schedule sweep failed");
            }
        }

        logger.LogInformation("Workflow schedule background service stopped");
    }

    private async Task RunDueSchedulesAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var workflowBridge = scope.ServiceProvider.GetRequiredService<IWorkflowBridge>();

        var now = DateTime.UtcNow;
        var schedules = await dbContext.WorkflowSchedules
            .Include(s => s.Workflow)
            .Where(s => !s.IsDeleted && s.IsActive)
            .ToListAsync(ct);

        var dirty = false;

        foreach (var schedule in schedules)
        {
            CronExpression cron;
            try
            {
                cron = CronExpression.Parse(schedule.CronExpression);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Schedule {Id} has an unparseable cron expression '{Cron}', skipping",
                    schedule.Id, schedule.CronExpression);
                continue;
            }

            // Newly created schedules have no NextRunAt yet (WorkflowSchedule's constructor never sets
            // it) — seed it here rather than treating "null" as "overdue", which would fire every
            // schedule immediately the first time this sweep runs after creation.
            if (schedule.NextRunAt is null)
            {
                schedule.SetNextRun(cron.GetNextOccurrence(now, TimeZoneInfo.Utc));
                dirty = true;
                continue;
            }

            if (schedule.NextRunAt > now)
                continue;

            dirty = true;

            if (schedule.Workflow is null || !schedule.Workflow.IsActive)
            {
                logger.LogWarning("Schedule {Id} is due but its workflow is missing or inactive; rescheduling without running", schedule.Id);
                schedule.SetNextRun(cron.GetNextOccurrence(now, TimeZoneInfo.Utc));
                continue;
            }

            try
            {
                await workflowBridge.StartWorkflowAsync(
                    schedule.WorkflowId, schedule.TenantId, schedule.Workflow.UserId, null, ct);
                schedule.RecordRun("Success");
                logger.LogInformation("Schedule {Id} triggered workflow {WorkflowId}", schedule.Id, schedule.WorkflowId);
            }
            catch (Exception ex)
            {
                schedule.RecordRun("Failed");
                logger.LogError(ex, "Schedule {Id} failed to trigger workflow {WorkflowId}", schedule.Id, schedule.WorkflowId);
            }

            schedule.SetNextRun(cron.GetNextOccurrence(now, TimeZoneInfo.Utc));
        }

        if (dirty)
            await dbContext.SaveChangesAsync(ct);
    }
}
