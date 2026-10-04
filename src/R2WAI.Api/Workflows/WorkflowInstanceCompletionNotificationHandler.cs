using Elsa.Mediator.Contracts;
using Elsa.Workflows;
using Elsa.Workflows.Notifications;
using Microsoft.EntityFrameworkCore;
using R2WAI.Api.Hubs;
using R2WAI.Application.Common;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Workflows;

/// <summary>
/// Closes a real gap: WorkflowInstance.Complete()/.Fail()/.Cancel() existed on the domain entity but
/// were never called anywhere — every WorkflowInstance stayed "Running" forever, regardless of what
/// actually happened to the underlying Elsa run (confirmed live: 44/44 instances stuck at Running).
/// StepStatusNotificationHandler already tracks each individual step via ActivityExecuted, but nothing
/// rolled that up to the instance level.
///
/// WorkflowFinished fires whenever Elsa's execution scheduler has no more work to do THIS PASS — which
/// includes suspending on a bookmark (e.g. ApprovalStepActivity awaiting a decision), not just true
/// completion. WorkflowSubStatus disambiguates: only Finished/Cancelled/Faulted are real terminal
/// states; Suspended means the workflow is still legitimately "Running" from R2WAI's perspective,
/// awaiting a resume (ResumeWorkflowAsync in WorkflowBridge). The IsDeleted-style idempotency guard
/// (only transition out of Running) protects against this notification firing more than once across a
/// multi-suspend/resume lifecycle.
/// </summary>
public class WorkflowInstanceCompletionNotificationHandler(ApplicationDbContext dbContext, IWorkflowStatusService statusService) :
    INotificationHandler<WorkflowFinished>
{
    public async Task HandleAsync(WorkflowFinished notification, CancellationToken cancellationToken)
    {
        var subStatus = notification.WorkflowExecutionContext.SubStatus;
        if (subStatus is not (WorkflowSubStatus.Finished or WorkflowSubStatus.Cancelled or WorkflowSubStatus.Faulted))
            return;

        if (!notification.WorkflowExecutionContext.Input.TryGetValue("InstanceId", out var instanceIdObj)
            || !Guid.TryParse(instanceIdObj?.ToString(), out var instanceId))
            return;

        // IgnoreQueryFilters: WorkflowFinished can fire from a background-triggered continuation
        // (WorkflowBridge.ContinueDelayedWorkflowAsync, invoked by WorkflowDelayResumeBackgroundService
        // with no HttpContext/ambient tenant) as well as from a live authenticated request — this
        // handler can't tell which. instanceId comes from Elsa's own WorkflowExecutionContext.Input,
        // set by WorkflowBridge itself, not from external/user input, so this is safe the same way the
        // background sweepers are. Without this, a workflow that completes via a Delay-step resume
        // never transitions out of Running — the exact "44/44 instances stuck at Running" bug this
        // handler was built to fix, reintroduced by P0-5's fail-closed filter for this one path.
        var instance = await dbContext.WorkflowInstances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == instanceId, cancellationToken);

        if (instance is null || instance.Status != WorkflowInstanceStatus.Running)
            return;

        switch (subStatus)
        {
            case WorkflowSubStatus.Finished:
                instance.Complete();
                break;
            case WorkflowSubStatus.Faulted:
                instance.Fail();
                break;
            case WorkflowSubStatus.Cancelled:
                instance.Cancel();
                break;
        }

        // Catch-all: an activity that throws (rather than returning with Status set to Faulted) never
        // triggers StepStatusNotificationHandler's ActivityExecuted branch — confirmed live, an Elsa
        // SendEmail failure shows up in Elsa's own WorkflowExecutionLogRecords as "Faulted" but
        // StepStatusNotificationHandler never sees it, leaving that WorkflowStepExecution row stuck at
        // Running forever (silently blocking RetryFailedStepAsync, which only looks for Failed rows).
        // Once the whole workflow has reached a terminal substatus, any step still marked Running for
        // this instance is — by definition — the one that actually caused it, so close it out here too.
        if (subStatus is WorkflowSubStatus.Faulted or WorkflowSubStatus.Cancelled)
        {
            var orphanedStep = await dbContext.WorkflowStepExecutions
                .Where(s => s.WorkflowInstanceId == instanceId && s.Status == WorkflowStepStatus.Running)
                .OrderBy(s => s.StepIndex)
                .FirstOrDefaultAsync(cancellationToken);

            orphanedStep?.Fail(subStatus == WorkflowSubStatus.Cancelled ? "Workflow cancelled" : "Step faulted (see workflow logs for details)");
        }

        DiagnosticsConfig.WorkflowExecutions.Add(1, new KeyValuePair<string, object?>("status", subStatus.ToString()));
        if (instance is { CompletedAt: { } completedAt, StartedAt: { } startedAt })
            DiagnosticsConfig.WorkflowExecutionDuration.Record(
                (completedAt - startedAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("status", subStatus.ToString()));

        await dbContext.SaveChangesAsync(cancellationToken);

        if (subStatus == WorkflowSubStatus.Finished)
        {
            await statusService.SendWorkflowCompletedAsync(instanceId, cancellationToken);
        }
        else if (subStatus is WorkflowSubStatus.Faulted or WorkflowSubStatus.Cancelled)
        {
            var error = subStatus == WorkflowSubStatus.Cancelled ? "Workflow cancelled" : "Workflow faulted (see workflow logs for details)";
            await statusService.SendWorkflowFailedAsync(instanceId, error, cancellationToken);
        }
    }
}
