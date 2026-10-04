using System.Text.Json;
using Elsa.Mediator.Contracts;
using Elsa.Workflows;
using Elsa.Workflows.Notifications;
using Microsoft.EntityFrameworkCore;
using R2WAI.Api.Hubs;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Workflows;

/// <summary>
/// Observes every activity's lifecycle across all Elsa workflow runs and reports the ones tagged with the
/// "step-{index}" Id convention (set by <see cref="Services.WorkflowBridge"/>) back to WorkflowStepExecution,
/// and pushes the same transitions live over StatusHub (see IWorkflowStatusService) so the automation
/// builder's node highlighting reflects real execution instead of never firing.
/// Resolves the R2WAI instance directly from the "InstanceId" workflow input (set by WorkflowBridge) rather
/// than via WorkflowInstance.ElsaInstanceId, since Elsa's own instance identifier is not guaranteed unique
/// per run in this environment (Elsa's own EF Core persistence tables are not provisioned here).
///
/// Also writes one AuditLog row per finished step (implementation plan follow-on to Phase 5 —
/// closes the "Workflow API Call step" audit-trail gap named in docs/audit/FEATURE-MATRIX.md,
/// for every step type, not just API Call: this handler already fires for all of them via Elsa's
/// ActivityExecuting/ActivityExecuted notifications, the same hook a repository research pass
/// confirmed exists generically — see docs/architecture/EXECUTION-AND-WORKFLOWS.md). One row per
/// step, at completion, mirroring ToolGateway's own one-row-per-call audit shape — not a second,
/// differently-structured audit convention.
/// </summary>
public class StepStatusNotificationHandler(ApplicationDbContext dbContext, IWorkflowStatusService statusService) :
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
        await statusService.SendStepStartedAsync(stepExec.WorkflowInstanceId, stepExec.StepName, stepExec.StepIndex, cancellationToken);
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

        var success = context.Status != ActivityStatus.Faulted;
        string? error = null;
        if (success)
        {
            var output = $"Step '{context.Activity.Name}' completed";
            stepExec.Complete(output);
            await WriteStepAuditAsync(stepExec, success: true, error: null, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await statusService.SendStepCompletedAsync(stepExec.WorkflowInstanceId, stepExec.StepName, stepExec.StepIndex, output, cancellationToken);
        }
        else
        {
            error = context.Exception?.Message ?? $"Step '{context.Activity.Name}' failed";
            stepExec.Fail(error);
            await WriteStepAuditAsync(stepExec, success: false, error, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await statusService.SendStepFailedAsync(stepExec.WorkflowInstanceId, stepExec.StepName, stepExec.StepIndex, error, cancellationToken);
        }
    }

    // One row per finished step, in the same SaveChangesAsync as the WorkflowStepExecution update
    // above (added to the same dbContext, not a separate isolated scope — this handler has no
    // long-lived ambient request the way a chat turn does, so there's no entanglement risk to
    // guard against here).
    private async Task WriteStepAuditAsync(WorkflowStepExecution stepExec, bool success, string? error, CancellationToken ct)
    {
        var instance = await dbContext.WorkflowInstances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == stepExec.WorkflowInstanceId, ct);
        if (instance is null) return;

        var metadata = JsonSerializer.Serialize(new
        {
            status = success ? "completed" : "failed",
            stepName = stepExec.StepName,
            stepType = stepExec.StepType,
            stepIndex = stepExec.StepIndex,
            attemptCount = stepExec.AttemptCount,
            error,
            durationMs = stepExec.StartedAt is { } startedAt ? (stepExec.CompletedAt - startedAt)?.TotalMilliseconds : null,
        });

        dbContext.AuditLogs.Add(new AuditLog(
            Guid.NewGuid(), instance.TenantId, AuditAction.Execute, "WorkflowStepExecution",
            stepExec.Id.ToString(), instance.InitiatedBy, metadata: metadata));
    }

    private async Task<WorkflowStepExecution?> ResolveStepExecutionAsync(ActivityExecutionContext context, CancellationToken ct)
    {
        if (!TryParseStepIndex(context.Activity.Id, out var stepIndex))
            return null;

        if (!context.WorkflowExecutionContext.Input.TryGetValue("InstanceId", out var instanceIdObj)
            || !Guid.TryParse(instanceIdObj?.ToString(), out var instanceId))
            return null;

        // IgnoreQueryFilters: this notification handler runs inside Elsa's own activity pipeline,
        // not a controller action — no ambient authenticated HttpContext, so the fail-closed
        // tenant filter would otherwise match zero rows regardless of the real instanceId/stepIndex.
        return await dbContext.WorkflowStepExecutions
            .IgnoreQueryFilters()
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
