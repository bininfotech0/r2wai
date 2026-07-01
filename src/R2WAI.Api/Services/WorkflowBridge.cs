using System.Text.Json;
using Elsa.Common.Models;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Models;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Messages;
using Microsoft.EntityFrameworkCore;
using R2WAI.Api.Workflows;
using R2WAI.Application.Features.Workflows.DTOs;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Services;

public interface IWorkflowBridge
{
    Task<(string ElsaInstanceId, Guid WorkflowInstanceId)> StartWorkflowAsync(
        Guid workflowId, Guid tenantId, Guid userId, string? data, CancellationToken ct);
    Task ResumeWorkflowAsync(string elsaInstanceId, string approvalRequestId, string approvalStatus, CancellationToken ct);
    Task<bool> RetryFailedStepAsync(Guid workflowInstanceId, CancellationToken ct);
}

public class WorkflowBridge : IWorkflowBridge
{
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly IWorkflowDefinitionPublisher _publisher;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkflowBridge> _logger;

    public WorkflowBridge(
        IWorkflowRuntime workflowRuntime,
        IWorkflowDefinitionPublisher publisher,
        ApplicationDbContext context,
        ILogger<WorkflowBridge> logger)
    {
        _workflowRuntime = workflowRuntime;
        _publisher = publisher;
        _context = context;
        _logger = logger;
    }

    public async Task<(string ElsaInstanceId, Guid WorkflowInstanceId)> StartWorkflowAsync(
        Guid workflowId, Guid tenantId, Guid userId, string? data, CancellationToken ct)
    {
        var workflowEntity = await _context.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.TenantId == tenantId, ct);

        if (workflowEntity is null)
            throw new InvalidOperationException($"Workflow {workflowId} not found for tenant {tenantId}");

        var steps = DeserializeSteps(workflowEntity.Steps);

        var sequence = new Sequence { Name = workflowEntity.Name };

        foreach (var step in steps.OrderBy(s => s.Order))
        {
            var activity = CreateActivityForStep(step, workflowId, tenantId, userId, data);
            if (activity is not null)
                sequence.Activities.Add(activity);
        }

        if (sequence.Activities.Count == 0)
        {
            sequence.Activities.Add(new WriteLine("No steps defined in workflow"));
        }

        var definitionId = $"r2wai-wf-{workflowId}";
        var definition = await _publisher.NewAsync(sequence, ct);
        definition.DefinitionId = definitionId;
        definition.Name = workflowEntity.Name;
        definition.Description = workflowEntity.Description ?? $"Auto-generated for R2WAI workflow {workflowId}";
        await _publisher.SaveDraftAsync(definition, ct);
        await _publisher.PublishAsync(definition, ct);

        var client = await _workflowRuntime.CreateClientAsync(definitionId);
        var result = await client.CreateAndRunInstanceAsync(new CreateAndRunWorkflowInstanceRequest
        {
            WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionId(definitionId, VersionOptions.Published),
            Input = new Dictionary<string, object>
            {
                ["WorkflowId"] = workflowEntity.Id.ToString(),
                ["TenantId"] = tenantId.ToString(),
                ["UserId"] = userId.ToString(),
                ["Data"] = data ?? string.Empty
            }
        }, ct);

        var instance = new R2WAI.Domain.Entities.WorkflowInstance(Guid.NewGuid(), workflowId, tenantId, userId, data);
        instance.SetElsaInstanceId(result.WorkflowInstanceId);
        _context.WorkflowInstances.Add(instance);

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps.OrderBy(s => s.Order).ElementAt(i);
            var stepType = (step.Name + " " + (step.Action ?? "")).ToLowerInvariant().Contains("approval")
                ? "Approval" : (step.Name + " " + (step.Action ?? "")).ToLowerInvariant().Contains("ai") ? "AI" : "Action";
            var stepExec = new R2WAI.Domain.Entities.WorkflowStepExecution(
                Guid.NewGuid(), instance.Id, i, step.Name, stepType);
            if (i == 0) stepExec.Start();
            _context.WorkflowStepExecutions.Add(stepExec);
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Started Elsa workflow instance {ElsaInstanceId} → R2WAI instance {InstanceId} for workflow {WorkflowId}",
            result.WorkflowInstanceId, instance.Id, workflowId);

        return (result.WorkflowInstanceId, instance.Id);
    }

    public async Task ResumeWorkflowAsync(string elsaInstanceId, string approvalRequestId, string approvalStatus, CancellationToken ct)
    {
        var client = await _workflowRuntime.CreateClientAsync(workflowInstanceId: elsaInstanceId);
        await client.RunInstanceAsync(new RunWorkflowInstanceRequest
        {
            Input = new Dictionary<string, object>
            {
                ["ApprovalStatus"] = approvalStatus
            }
        }, ct);

        _logger.LogInformation(
            "Resumed Elsa workflow instance {InstanceId} for approval {ApprovalRequestId} with status {Status}",
            elsaInstanceId, approvalRequestId, approvalStatus);
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

    private static IActivity? CreateActivityForStep(
        WorkflowStepDto step,
        Guid workflowId,
        Guid tenantId,
        Guid userId,
        string? data)
    {
        var stepNameLower = (step.Name + " " + (step.Action ?? string.Empty)).ToLowerInvariant();

        if (stepNameLower.Contains("approval"))
        {
            return new ApprovalStepActivity
            {
                Name = step.Name,
                TenantId = new Input<string>(tenantId.ToString()),
                WorkflowInstanceId = new Input<string>(workflowId.ToString()),
                WorkflowDefinitionId = new Input<string>(workflowId.ToString()),
                RequesterId = new Input<string>(userId.ToString()),
                Data = new Input<string?>(data)
            };
        }

        if (stepNameLower.Contains("ai") || stepNameLower.Contains("generate"))
        {
            var prompt = !string.IsNullOrEmpty(step.Action)
                ? step.Action
                : $"Execute step: {step.Name}";

            return new InvokeSemanticKernelActivity
            {
                Name = step.Name,
                Prompt = new Input<string>(prompt),
                SystemPrompt = new Input<string?>(
                    $"You are executing workflow step '{step.Name}'. Assigned role: {step.AssignedRole ?? "System"}.")
            };
        }

        return new WriteLine($"Step: {step.Name} | Action: {step.Action ?? "none"} | Role: {step.AssignedRole ?? "System"}")
        {
            Name = step.Name
        };
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
