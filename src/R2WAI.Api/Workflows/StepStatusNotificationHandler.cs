using Elsa.Mediator.Contracts;
using Elsa.Workflows;
using Elsa.Workflows.Notifications;
using Microsoft.EntityFrameworkCore;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Workflows;

/// <summary>
/// Observes every activity's lifecycle across all Elsa workflow runs and reports the ones tagged with the
/// "step-{index}" Id convention (set by <see cref="Services.WorkflowBridge"/>) back to WorkflowStepExecution.
/// Resolves the R2WAI instance directly from the "InstanceId" workflow input (set by WorkflowBridge) rather
/// than via WorkflowInstance.ElsaInstanceId, since Elsa's own instance identifier is not guaranteed unique
/// per run in this environment (Elsa's own EF Core persistence tables are not provisioned here).
/// </summary>
public class StepStatusNotificationHandler(ApplicationDbContext dbContext) :
    INotificationHandler<ActivityExecuting>,
    INotificationHandler<ActivityExecuted>
{
    public async Task HandleAsync(ActivityExecuting notification, CancellationToken cancellationToken)
    {
        var stepExec = await ResolveStepExecutionAsync(notification.ActivityExecutionContext, cancellationToken);
        if (stepExec is null)
            return;

        stepExec.Start();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task HandleAsync(ActivityExecuted notification, CancellationToken cancellationToken)
    {
        var context = notification.ActivityExecutionContext;

        // "Executed" fires every time an activity's execution pass returns control, including when it
        // merely creates a bookmark and suspends (e.g. ApprovalStepActivity awaiting a decision) -- Status
        // is still "Running" in that case. Only Completed/Faulted represent the step actually finishing.
        if (context.Status is not (ActivityStatus.Completed or ActivityStatus.Faulted))
            return;

        var stepExec = await ResolveStepExecutionAsync(context, cancellationToken);
        if (stepExec is null)
            return;

        if (context.Status == ActivityStatus.Faulted)
        {
            var error = context.Exception?.Message ?? $"Step '{context.Activity.Name}' failed";
            stepExec.Fail(error);
        }
        else
        {
            stepExec.Complete($"Step '{context.Activity.Name}' completed");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<WorkflowStepExecution?> ResolveStepExecutionAsync(ActivityExecutionContext context, CancellationToken ct)
    {
        if (!TryParseStepIndex(context.Activity.Id, out var stepIndex))
            return null;

        if (!context.WorkflowExecutionContext.Input.TryGetValue("InstanceId", out var instanceIdObj)
            || !Guid.TryParse(instanceIdObj?.ToString(), out var instanceId))
            return null;

        return await dbContext.WorkflowStepExecutions
            .FirstOrDefaultAsync(s => s.WorkflowInstanceId == instanceId && s.StepIndex == stepIndex, ct);
    }

    private static bool TryParseStepIndex(string? activityId, out int stepIndex)
    {
        stepIndex = -1;
        return activityId is not null
            && activityId.StartsWith("step-", StringComparison.Ordinal)
            && int.TryParse(activityId.AsSpan("step-".Length), out stepIndex);
    }
}
