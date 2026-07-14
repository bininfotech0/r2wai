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
using Elsa.Workflows.Runtime.Filters;
using Elsa.Workflows.Runtime.Messages;
using Microsoft.EntityFrameworkCore;
using R2WAI.Api.Workflows;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Workflows.DTOs;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Services;

public class WorkflowBridge : IWorkflowBridge
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IWorkflowDefinitionPublisher _publisher;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkflowBridge> _logger;

    public WorkflowBridge(
        IWorkflowRuntime workflowRuntime,
        IWorkflowDefinitionPublisher publisher,
        IBookmarkStore bookmarkStore,
        ApplicationDbContext context,
        ILogger<WorkflowBridge> logger)
    {
        _workflowRuntime = workflowRuntime;
        _publisher = publisher;
        _bookmarkStore = bookmarkStore;
        _context = context;
        _logger = logger;
    }

    public async Task<(string ElsaInstanceId, Guid WorkflowInstanceId)> StartWorkflowAsync(
        Guid workflowId, Guid tenantId, Guid userId, string? data, CancellationToken ct, Guid? existingInstanceId = null)
    {
        var workflowEntity = await _context.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.TenantId == tenantId, ct);

        if (workflowEntity is null)
            throw new InvalidOperationException($"Workflow {workflowId} not found for tenant {tenantId}");

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

        var flowchart = new Flowchart { Name = workflowEntity.Name };
        var nodesByIndex = new Dictionary<int, IActivity>();
        var nodesByName = new Dictionary<string, IActivity>(StringComparer.OrdinalIgnoreCase);
        var nextStepsByIndex = new Dictionary<int, List<string>?>();

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var stepType = StepActivityFactory.ClassifyStepType(step, _logger);

            var stepExec = new R2WAI.Domain.Entities.WorkflowStepExecution(
                Guid.NewGuid(), instanceId, i, step.Name, stepType);
            _context.WorkflowStepExecutions.Add(stepExec);

            var node = StepActivityFactory.CreateActivityForStep(step, workflowId, tenantId, userId, instanceId, data, _logger);
            node.Id = $"step-{i}";
            node.Name = step.Name;

            flowchart.Activities.Add(node);
            nodesByIndex[i] = node;
            nodesByName[step.Name] = node;
            nextStepsByIndex[i] = step.Config?.Deserialize<NextStepsOnlyDto>(StepPayloadJsonOptions)?.NextSteps;
        }

        if (nodesByIndex.Count > 0)
            flowchart.Start = nodesByIndex[0];

        // Build the graph: an explicit Config.NextSteps list fans out to those named steps (enabling
        // real parallel branches); with none specified, fall back to the next step in Order (today's
        // linear behavior, so pre-existing saved workflows keep working unchanged). A step named as the
        // next step by more than one source becomes an implicit join (Elsa's Flowchart waits for all
        // incoming connections by default).
        for (var i = 0; i < steps.Count; i++)
        {
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
                        _logger.LogWarning("Step '{Step}' lists unknown next step '{Next}'", steps[i].Name, name);
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

        await _context.SaveChangesAsync(ct);

        var definitionId = $"r2wai-wf-{workflowId}";
        var definition = await _publisher.NewAsync(flowchart, ct);
        definition.DefinitionId = definitionId;
        definition.Name = workflowEntity.Name;
        definition.Description = workflowEntity.Description ?? $"Auto-generated for R2WAI workflow {workflowId}";
        await _publisher.SaveDraftAsync(definition, ct);
        await _publisher.PublishAsync(definition, ct);

        // CreateClientAsync(ct) with no instance id generates a fresh Elsa instance id for this run.
        // Passing definitionId here (the old code) told Elsa to target/reuse THAT literal string as the
        // instance id on every single execution of this workflow, so every run of the same workflow
        // collided on the same Elsa instance -- explaining stale bookmarks, wrong instance lookups, etc.
        var client = await _workflowRuntime.CreateClientAsync(ct);
        var result = await client.CreateAndRunInstanceAsync(new CreateAndRunWorkflowInstanceRequest
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

        instance.SetElsaInstanceId(result.WorkflowInstanceId);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Started Elsa workflow instance {ElsaInstanceId} → R2WAI instance {InstanceId} for workflow {WorkflowId}",
            result.WorkflowInstanceId, instanceId, workflowId);

        return (result.WorkflowInstanceId, instanceId);
    }

    public async Task ResumeWorkflowAsync(string elsaInstanceId, string approvalRequestId, string approvalStatus, CancellationToken ct)
    {
        // RunInstanceAsync needs to know WHICH suspended bookmark to resume -- passing plain Input alone
        // (as this used to) does not target ApprovalStepActivity's bookmark, so the instance never actually
        // advances. Look up the pending bookmark for this instance and resume it explicitly by Id.
        var bookmarks = await _bookmarkStore.FindManyAsync(
            new BookmarkFilter { WorkflowInstanceId = elsaInstanceId }, ct);
        var bookmark = bookmarks.FirstOrDefault();

        if (bookmark is null)
        {
            _logger.LogWarning(
                "No pending bookmark found for Elsa instance {InstanceId}; approval {ApprovalRequestId} may not resume the workflow",
                elsaInstanceId, approvalRequestId);
        }

        var client = await _workflowRuntime.CreateClientAsync(workflowInstanceId: elsaInstanceId);
        await client.RunInstanceAsync(new RunWorkflowInstanceRequest
        {
            BookmarkId = bookmark?.Id,
            Input = new Dictionary<string, object>
            {
                ["ApprovalStatus"] = approvalStatus
            }
        }, ct);

        _logger.LogInformation(
            "Resumed Elsa workflow instance {InstanceId} for approval {ApprovalRequestId} with status {Status} (bookmark {BookmarkId})",
            elsaInstanceId, approvalRequestId, approvalStatus, bookmark?.Id ?? "none");
    }

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

        var instance = await _context.WorkflowInstances
            .FirstOrDefaultAsync(i => i.Id == workflowInstanceId, ct);

        if (instance?.ElsaInstanceId is null)
        {
            _logger.LogWarning("No Elsa instance ID for R2WAI instance {InstanceId}", workflowInstanceId);
            return false;
        }

        failedStep.Start();
        await _context.SaveChangesAsync(ct);

        try
        {
            var client = await _workflowRuntime.CreateClientAsync(workflowInstanceId: instance.ElsaInstanceId);
            await client.RunInstanceAsync(new RunWorkflowInstanceRequest
            {
                Input = new Dictionary<string, object>
                {
                    ["RetryStep"] = failedStep.StepName,
                    ["RetryAttempt"] = "true"
                }
            }, ct);

            failedStep.Complete("Retry successful");
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Retried step {StepName} for instance {InstanceId}", failedStep.StepName, workflowInstanceId);
            return true;
        }
        catch (Exception ex)
        {
            failedStep.Fail($"Retry failed: {ex.Message}");
            await _context.SaveChangesAsync(ct);

            _logger.LogError(ex, "Retry failed for step {StepName} in instance {InstanceId}", failedStep.StepName, workflowInstanceId);
            return false;
        }
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
            return JsonSerializer.Deserialize<List<WorkflowStepDto>>(stepsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
