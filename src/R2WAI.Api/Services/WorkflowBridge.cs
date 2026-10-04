using System.Text.Json;
using Elsa.Common.Models;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Activities.Flowchart.Models;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Messages;
using Microsoft.EntityFrameworkCore;
using R2WAI.Api.Workflows;
using R2WAI.Api.Workflows.NodeProviders;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Workflows.DTOs;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Services;

public class WorkflowBridge : IWorkflowBridge
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IWorkflowDefinitionPublisher _publisher;
    private readonly ApplicationDbContext _context;
    private readonly StepActivityFactory _stepActivityFactory;
    private readonly ILogger<WorkflowBridge> _logger;

    public WorkflowBridge(
        IWorkflowRuntime workflowRuntime,
        IWorkflowDefinitionPublisher publisher,
        ApplicationDbContext context,
        StepActivityFactory stepActivityFactory,
        ILogger<WorkflowBridge> logger)
    {
        _workflowRuntime = workflowRuntime;
        _publisher = publisher;
        _context = context;
        _stepActivityFactory = stepActivityFactory;
        _logger = logger;
    }

    public async Task<(string ElsaInstanceId, Guid WorkflowInstanceId)> StartWorkflowAsync(
        Guid workflowId, Guid tenantId, Guid userId, string? data, CancellationToken ct, Guid? existingInstanceId = null)
    {
        var workflowEntity = await _context.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.TenantId == tenantId, ct);

        // NotFoundException (404), not InvalidOperationException (409): a missing workflow ID is
        // exactly what it says — nothing for the caller to "resolve a conflict" over, so it must
        // not share the exception middleware's domain-guard-conflict mapping.
        if (workflowEntity is null)
            throw new NotFoundException(nameof(Domain.Entities.Workflow), workflowId);

        var steps = DeserializeSteps(workflowEntity.Steps).OrderBy(s => s.Order).ToList();

        // Resolve (and persist) the R2WAI instance + its step rows BEFORE handing control to Elsa,
        // since Elsa can execute activities synchronously up to the first bookmark.
        R2WAI.Domain.Entities.WorkflowInstance? instance = null;
        if (existingInstanceId is not null)
            instance = await _context.WorkflowInstances.FirstOrDefaultAsync(i => i.Id == existingInstanceId, ct);

        if (instance is null)
        {
            instance = new R2WAI.Domain.Entities.WorkflowInstance(
                existingInstanceId ?? Guid.NewGuid(), workflowId, tenantId, userId, data);
            _context.WorkflowInstances.Add(instance);
        }

        var instanceId = instance.Id;

        foreach (var step in steps)
        {
            var stepType = StepActivityFactory.ClassifyStepType(step, _logger);
            var stepExec = new R2WAI.Domain.Entities.WorkflowStepExecution(
                Guid.NewGuid(), instanceId, step.Order, step.Name, stepType);
            _context.WorkflowStepExecutions.Add(stepExec);
        }

        await _context.SaveChangesAsync(ct);

        var definitionId = $"r2wai-wf-{workflowId}";
        await RunFromAsync(workflowEntity, instance, steps, definitionId, ct);

        _logger.LogInformation(
            "Started R2WAI instance {InstanceId} for workflow {WorkflowId} (Elsa instance {ElsaInstanceId})",
            instanceId, workflowId, instance.ElsaInstanceId ?? "none — workflow has no steps or starts with a Delay");

        return (instance.ElsaInstanceId ?? string.Empty, instanceId);
    }

    // Runs `steps` starting from its first entry, stopping (without ever running it as a real Elsa
    // activity) at the first Delay step encountered — see the class-level remarks above
    // ResumeWorkflowAsync for why Delay steps are handled this way instead of via Elsa's native
    // Delay+scheduler. Schedules R2WAI's own continuation for that Delay step via
    // WorkflowInstance.ScheduleDelayedResume, later picked up by WorkflowDelayResumeBackgroundService.
    // If `steps` is empty, or nothing ran and there was no Delay to schedule either, the instance is
    // marked Completed directly. Used by every entry point that starts or continues a run: initial
    // start, approval-resume, failed-step retry, and delayed continuation.
    private async Task RunFromAsync(
        Domain.Entities.Workflow workflowEntity, R2WAI.Domain.Entities.WorkflowInstance instance,
        List<WorkflowStepDto> steps, string definitionId, CancellationToken ct)
    {
        if (steps.Count == 0)
        {
            instance.Complete();
            await _context.SaveChangesAsync(ct);
            return;
        }

        var delayStep = steps.FirstOrDefault(s => StepActivityFactory.ClassifyStepType(s, _logger) == "Delay");
        var stepsToRun = delayStep is null ? steps : steps.Where(s => s.Order < delayStep.Order).ToList();

        if (stepsToRun.Count > 0)
        {
            var flowchart = BuildFlowchart(
                stepsToRun, workflowEntity.Name, workflowEntity.Id, instance.TenantId, instance.InitiatedBy, instance.Id, instance.Data);
            var result = await PublishAndRunAsync(
                flowchart, definitionId, workflowEntity, instance.TenantId, instance.InitiatedBy, instance.Id, instance.Data, ct);
            instance.SetElsaInstanceId(result.WorkflowInstanceId);
        }

        if (delayStep is not null)
        {
            var delayStepExec = await _context.WorkflowStepExecutions
                .FirstOrDefaultAsync(s => s.WorkflowInstanceId == instance.Id && s.StepIndex == delayStep.Order, ct);
            delayStepExec?.Start();

            var config = delayStep.Config?.Deserialize<StepConfigDto>(StepPayloadJsonOptions);
            var resumeAt = DateTime.UtcNow.Add(DelayNodeProvider.ComputeDelay(config));
            instance.ScheduleDelayedResume(resumeAt, delayStep.Order);

            _logger.LogInformation(
                "R2WAI instance {InstanceId} reached Delay step {StepIndex}, scheduled to resume at {ResumeAt:o}",
                instance.Id, delayStep.Order, resumeAt);
        }

        await _context.SaveChangesAsync(ct);
    }

    // Builds a Flowchart's activities + connections for the given steps (which may be the workflow's
    // full step list, or a tail slice continuing after a suspended step — see ContinueAfterSuspendAsync).
    // Each activity keeps its Id as "step-{step.Order}" (the ORIGINAL index, not a renumbered one) so
    // StepStatusNotificationHandler/WorkflowInstanceCompletionNotificationHandler — which correlate
    // purely by that Id + the "InstanceId" workflow Input, never by Elsa's own instance/context
    // identity — keep resolving to the correct pre-existing WorkflowStepExecution row regardless of
    // which Elsa run (the original, or a later continuation) an activity actually executes under.
    private Flowchart BuildFlowchart(
        List<WorkflowStepDto> steps, string workflowName,
        Guid workflowId, Guid tenantId, Guid userId, Guid instanceId, string? data)
    {
        var flowchart = new Flowchart { Name = workflowName };
        var nodesByIndex = new Dictionary<int, IActivity>();
        var nodesByName = new Dictionary<string, IActivity>(StringComparer.OrdinalIgnoreCase);
        var nextStepsByIndex = new Dictionary<int, List<string>?>();

        foreach (var step in steps)
        {
            var i = step.Order;
            var node = _stepActivityFactory.CreateActivityForStep(step, workflowId, tenantId, userId, instanceId, data, _logger);
            node.Id = $"step-{i}";
            node.Name = step.Name;

            flowchart.Activities.Add(node);
            nodesByIndex[i] = node;
            nodesByName[step.Name] = node;
            nextStepsByIndex[i] = step.Config?.Deserialize<NextStepsOnlyDto>(StepPayloadJsonOptions)?.NextSteps;
        }

        if (nodesByIndex.Count > 0)
            flowchart.Start = nodesByIndex[steps[0].Order];

        // Build the graph: an explicit Config.NextSteps list fans out to those named steps (enabling
        // real parallel branches); with none specified, fall back to the next step in Order (today's
        // linear behavior, so pre-existing saved workflows keep working unchanged). A step named as the
        // next step by more than one source becomes an implicit join (Elsa's Flowchart waits for all
        // incoming connections by default).
        foreach (var step in steps)
        {
            var i = step.Order;
            if (!nodesByIndex.TryGetValue(i, out var sourceNode))
                continue;

            var nextNames = nextStepsByIndex[i]?
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            if (nextNames is { Count: > 0 })
            {
                foreach (var name in nextNames)
                {
                    if (nodesByName.TryGetValue(name, out var targetNode))
                        flowchart.Connections.Add(new Connection(sourceNode, targetNode));
                    else
                        _logger.LogWarning("Step '{Step}' lists unknown next step '{Next}'", step.Name, name);
                }
            }
            else if (nodesByIndex.TryGetValue(i + 1, out var nextByOrder))
            {
                flowchart.Connections.Add(new Connection(sourceNode, nextByOrder));
            }
        }

        if (flowchart.Activities.Count == 0)
        {
            var placeholder = new WriteLine("No steps defined in workflow") { Id = "step-placeholder" };
            flowchart.Activities.Add(placeholder);
            flowchart.Start = placeholder;
        }

        return flowchart;
    }

    // Publishes the given flowchart under definitionId and starts a fresh Elsa run of it.
    // CreateClientAsync(ct) with no instance id generates a fresh Elsa instance id for this run.
    // Passing definitionId here (the old code) told Elsa to target/reuse THAT literal string as the
    // instance id on every single execution of this workflow, so every run of the same workflow
    // collided on the same Elsa instance -- explaining stale bookmarks, wrong instance lookups, etc.
    private async Task<RunWorkflowInstanceResponse> PublishAndRunAsync(
        Flowchart flowchart, string definitionId, Domain.Entities.Workflow workflowEntity,
        Guid tenantId, Guid userId, Guid instanceId, string? data, CancellationToken ct)
    {
        var definition = await _publisher.NewAsync(flowchart, ct);
        definition.DefinitionId = definitionId;
        definition.Name = workflowEntity.Name;
        definition.Description = workflowEntity.Description ?? $"Auto-generated for R2WAI workflow {workflowEntity.Id}";
        await _publisher.SaveDraftAsync(definition, ct);
        await _publisher.PublishAsync(definition, ct);

        var client = await _workflowRuntime.CreateClientAsync(ct);
        return await client.CreateAndRunInstanceAsync(new CreateAndRunWorkflowInstanceRequest
        {
            WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionId(definitionId, VersionOptions.Published),
            Input = new Dictionary<string, object>
            {
                ["WorkflowId"] = workflowEntity.Id.ToString(),
                ["TenantId"] = tenantId.ToString(),
                ["UserId"] = userId.ToString(),
                ["InstanceId"] = instanceId.ToString(),
                ["Data"] = data ?? string.Empty
            }
        }, ct);
    }

    // Elsa 3.7.0's bookmark-based resume (client.RunInstanceAsync targeting a BookmarkId) does not
    // work for these dynamically-built-and-republished-per-run Flowchart definitions: reproducibly,
    // both this method's old implementation AND Elsa's own native scheduled resume (e.g. a Delay
    // activity's timer) fail identically with "Could not find activity execution context ... for
    // bookmark" and silently no-op — confirmed by inspecting Elsa's own persisted WorkflowState and
    // execution-log tables directly, not just this app's logs. Root cause appears to be structural
    // (how Elsa reconstructs a suspended Flowchart's activity-context tree from a cold/persisted
    // state), not a call-site mistake, and no fix exists in Elsa 3.7.1 either.
    //
    // Workaround: don't ask Elsa to resume the *same* run at all. Mark the suspended step Completed
    // directly (R2WAI's own WorkflowStepExecution rows are the authoritative execution record, per
    // StepStatusNotificationHandler's doc comment), then start a brand-new Elsa run containing only
    // the remaining steps, reusing their original "step-{index}" ids and the same R2WAI InstanceId in
    // Input so step-tracking and completion notifications keep resolving correctly (see BuildFlowchart).
    public async Task ResumeWorkflowAsync(string elsaInstanceId, string approvalRequestId, string approvalStatus, CancellationToken ct)
    {
        var instance = await _context.WorkflowInstances
            .FirstOrDefaultAsync(i => i.ElsaInstanceId == elsaInstanceId, ct);

        if (instance is null)
        {
            _logger.LogWarning(
                "No R2WAI WorkflowInstance found for Elsa instance {ElsaInstanceId}; approval {ApprovalRequestId} cannot resume the workflow",
                elsaInstanceId, approvalRequestId);
            return;
        }

        var suspendedStep = await _context.WorkflowStepExecutions
            .Where(s => s.WorkflowInstanceId == instance.Id && s.Status == Domain.Enums.WorkflowStepStatus.Running)
            .OrderBy(s => s.StepIndex)
            .FirstOrDefaultAsync(ct);

        if (suspendedStep is null)
        {
            _logger.LogWarning(
                "No suspended step found for R2WAI instance {InstanceId}; approval {ApprovalRequestId} cannot resume the workflow",
                instance.Id, approvalRequestId);
            return;
        }

        suspendedStep.Complete($"Approval {approvalStatus}");

        // Only an explicit approval may continue the run. This used to run every remaining step for any
        // decision, so a rejected (or otherwise unapproved) request still executed the work it was
        // meant to gate.
        if (!string.Equals(approvalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            await StopRunAfterUnapprovedDecisionAsync(instance, suspendedStep, approvalStatus, approvalRequestId, ct);
            return;
        }

        await _context.SaveChangesAsync(ct);

        var workflowEntity = await _context.Workflows.FirstOrDefaultAsync(w => w.Id == instance.WorkflowId, ct);
        if (workflowEntity is null)
        {
            _logger.LogWarning("Workflow {WorkflowId} not found while resuming instance {InstanceId}", instance.WorkflowId, instance.Id);
            return;
        }

        var remainingSteps = DeserializeSteps(workflowEntity.Steps)
            .Where(s => s.Order > suspendedStep.StepIndex)
            .OrderBy(s => s.Order)
            .ToList();

        var definitionId = remainingSteps.Count > 0
            ? $"r2wai-wf-{workflowEntity.Id}-continue-{instance.Id}-{remainingSteps[0].Order}"
            : string.Empty;
        await RunFromAsync(workflowEntity, instance, remainingSteps, definitionId, ct);

        _logger.LogInformation(
            "Resumed R2WAI instance {InstanceId} after approval {ApprovalRequestId} (status {Status}), continuing from step index {StepIndex}",
            instance.Id, approvalRequestId, approvalStatus, suspendedStep.StepIndex + 1);
    }

    // A rejected/cancelled/expired approval ends the run: the decided step keeps its outcome, every step
    // that had not started is Skipped, and the instance is Cancelled — nothing after the approval runs.
    private async Task StopRunAfterUnapprovedDecisionAsync(
        R2WAI.Domain.Entities.WorkflowInstance instance, R2WAI.Domain.Entities.WorkflowStepExecution decidedStep,
        string approvalStatus, string approvalRequestId, CancellationToken ct)
    {
        var notStarted = await _context.WorkflowStepExecutions
            .Where(s => s.WorkflowInstanceId == instance.Id
                && s.StepIndex > decidedStep.StepIndex
                && s.Status == Domain.Enums.WorkflowStepStatus.Pending)
            .ToListAsync(ct);

        foreach (var step in notStarted)
            step.Skip();

        var wasRunning = instance.Status == Domain.Enums.WorkflowInstanceStatus.Running;
        if (wasRunning)
            instance.Cancel();

        await _context.SaveChangesAsync(ct);

        if (wasRunning)
        {
            R2WAI.Application.Common.DiagnosticsConfig.WorkflowExecutions.Add(1,
                new KeyValuePair<string, object?>("status", "Cancelled"));
        }

        _logger.LogInformation(
            "R2WAI instance {InstanceId} stopped at step index {StepIndex}: approval {ApprovalRequestId} was {Status}; {Skipped} later step(s) skipped",
            instance.Id, decidedStep.StepIndex, approvalRequestId, approvalStatus, notStarted.Count);
    }

    // Shares ResumeWorkflowAsync's rebuild-and-continue workaround for the same underlying Elsa
    // bookmark-resume limitation. Unlike approval-resume, this re-executes the failed step itself
    // (not just what follows it), so the failed step's own WorkflowStepExecution row isn't marked
    // Complete here — it's left for StepStatusNotificationHandler to update naturally as the rebuilt
    // flowchart actually re-runs it (Start() on ActivityExecuting, Complete()/Fail() on the real
    // outcome), same as any fresh run — rather than optimistically assuming success like the old code did.
    public async Task<bool> RetryFailedStepAsync(Guid workflowInstanceId, CancellationToken ct)
    {
        var failedStep = await _context.WorkflowStepExecutions
            .Where(s => s.WorkflowInstanceId == workflowInstanceId && s.Status == Domain.Enums.WorkflowStepStatus.Failed)
            .OrderBy(s => s.StepIndex)
            .FirstOrDefaultAsync(ct);

        if (failedStep is null)
        {
            _logger.LogWarning("No failed step found for instance {InstanceId}", workflowInstanceId);
            return false;
        }

        var instance = await _context.WorkflowInstances.FirstOrDefaultAsync(i => i.Id == workflowInstanceId, ct);
        if (instance is null)
        {
            _logger.LogWarning("R2WAI instance {InstanceId} not found", workflowInstanceId);
            return false;
        }

        var workflowEntity = await _context.Workflows.FirstOrDefaultAsync(w => w.Id == instance.WorkflowId, ct);
        if (workflowEntity is null)
        {
            _logger.LogWarning("Workflow {WorkflowId} not found while retrying instance {InstanceId}", instance.WorkflowId, instance.Id);
            return false;
        }

        var remainingSteps = DeserializeSteps(workflowEntity.Steps)
            .Where(s => s.Order >= failedStep.StepIndex)
            .OrderBy(s => s.Order)
            .ToList();

        if (remainingSteps.Count == 0)
        {
            _logger.LogWarning(
                "Failed step {StepIndex} for instance {InstanceId} no longer exists in the current workflow definition",
                failedStep.StepIndex, instance.Id);
            return false;
        }

        try
        {
            // Must happen before RunFromAsync — WorkflowInstanceCompletionNotificationHandler's
            // idempotency guard only reacts while the instance is Running, and a Failed instance
            // stays Failed until something explicitly says otherwise.
            instance.Retry();

            var definitionId = $"r2wai-wf-{workflowEntity.Id}-retry-{instance.Id}-{remainingSteps[0].Order}";
            await RunFromAsync(workflowEntity, instance, remainingSteps, definitionId, ct);

            _logger.LogInformation(
                "Retrying R2WAI instance {InstanceId} from failed step {StepIndex}", instance.Id, failedStep.StepIndex);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart instance {InstanceId} from failed step {StepIndex}", instance.Id, failedStep.StepIndex);
            return false;
        }
    }

    // Called by WorkflowDelayResumeBackgroundService once a Delay step's scheduled wait has elapsed —
    // the counterpart to ResumeWorkflowAsync's approval-driven continuation, same rebuild-and-continue
    // workaround (see that method's doc comment).
    public async Task<bool> ContinueDelayedWorkflowAsync(Guid workflowInstanceId, CancellationToken ct)
    {
        // IgnoreQueryFilters: this is only ever called from WorkflowDelayResumeBackgroundService, a
        // background sweeper with no HttpContext/ambient tenant (P0-5's fail-closed filter would
        // otherwise always miss here — confirmed live: without this, every Delay step permanently
        // stalls its workflow, since the sweeper's own atomic claim already cleared PendingResumeAt
        // before calling in, so a failed lookup here means the instance can never be retried either).
        // workflowInstanceId comes from that sweeper's own claim query, not from any external/user
        // input, so bypassing the ambient filter here carries no cross-tenant read risk.
        var instance = await _context.WorkflowInstances.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == workflowInstanceId, ct);
        if (instance?.PendingResumeStepIndex is not { } delayStepIndex)
        {
            _logger.LogWarning("R2WAI instance {InstanceId} has no pending delayed resume", workflowInstanceId);
            return false;
        }

        var delayStepExec = await _context.WorkflowStepExecutions
            .FirstOrDefaultAsync(s => s.WorkflowInstanceId == instance.Id && s.StepIndex == delayStepIndex, ct);
        delayStepExec?.Complete("Delay elapsed");
        instance.ClearPendingResume();

        // Same IgnoreQueryFilters reasoning as the instance lookup above.
        var workflowEntity = await _context.Workflows.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == instance.WorkflowId, ct);
        if (workflowEntity is null)
        {
            _logger.LogWarning("Workflow {WorkflowId} not found while continuing delayed instance {InstanceId}", instance.WorkflowId, instance.Id);
            await _context.SaveChangesAsync(ct);
            return false;
        }

        var remainingSteps = DeserializeSteps(workflowEntity.Steps)
            .Where(s => s.Order > delayStepIndex)
            .OrderBy(s => s.Order)
            .ToList();

        var definitionId = remainingSteps.Count > 0
            ? $"r2wai-wf-{workflowEntity.Id}-delay-{instance.Id}-{remainingSteps[0].Order}"
            : string.Empty;
        await RunFromAsync(workflowEntity, instance, remainingSteps, definitionId, ct);

        _logger.LogInformation("Continued R2WAI instance {InstanceId} after Delay step {StepIndex} elapsed", instance.Id, delayStepIndex);
        return true;
    }

    private static readonly JsonSerializerOptions StepPayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class NextStepsOnlyDto
    {
        public List<string>? NextSteps { get; set; }
    }

    private static List<WorkflowStepDto> DeserializeSteps(string? stepsJson)
    {
        if (string.IsNullOrEmpty(stepsJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<WorkflowStepDto>>(stepsJson, StepPayloadJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
