using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.Services;

namespace R2WAI.Infrastructure.AI.Plugins;

public class WorkflowPlugin
{
    private readonly IApprovalService _approvalService;
    private readonly IWorkflowService _workflowService;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;
    private readonly ILogger<WorkflowPlugin> _logger;

    public WorkflowPlugin(
        IApprovalService approvalService,
        IWorkflowService workflowService,
        ICurrentUserService currentUser,
        INotificationService notificationService,
        ILogger<WorkflowPlugin> logger)
    {
        _approvalService = approvalService;
        _workflowService = workflowService;
        _currentUser = currentUser;
        _notificationService = notificationService;
        _logger = logger;
    }

    [KernelFunction("submit_approval_request")]
    [Description("Submit a new approval request for a workflow instance")]
    [return: Description("The result of the approval request submission")]
    public async Task<string> SubmitApprovalRequestAsync(
        [Description("The workflow instance ID to approve")] Guid workflowInstanceId,
        [Description("Optional data/context for the approval")] string? data = null,
        CancellationToken ct = default)
    {
        try
        {
            var tenantId = _currentUser.TenantId ?? throw new InvalidOperationException("User tenant not found");
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("User ID not found");

            var instance = await _workflowService.GetWorkflowInstanceByIdAsync(workflowInstanceId, ct);

            var requestId = await _approvalService.CreateApprovalRequestAsync(
                tenantId,
                workflowInstanceId,
                instance.WorkflowId,
                userId,
                data,
                ct);

            return $"Approval request submitted successfully. Approval request ID: {requestId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit approval request for instance {InstanceId}", workflowInstanceId);
            return $"Error submitting approval request: {ex.Message}";
        }
    }

    [KernelFunction("get_workflow_status")]
    [Description("Get the current status of a workflow instance")]
    [return: Description("The workflow status information")]
    public async Task<string> GetWorkflowStatusAsync(
        [Description("The workflow instance ID")] Guid instanceId,
        CancellationToken ct = default)
    {
        try
        {
            var instance = await _workflowService.GetWorkflowInstanceByIdAsync(instanceId, ct);
            return System.Text.Json.JsonSerializer.Serialize(instance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflow status for instance {InstanceId}", instanceId);
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("notify_approver")]
    [Description("Send a notification to a workflow approver")]
    [return: Description("Notification result")]
    public async Task<string> NotifyApproverAsync(
        [Description("The workflow instance ID")] Guid instanceId,
        [Description("The approver's email")] string approverEmail,
        [Description("The approval message")] string message,
        CancellationToken ct = default)
    {
        try
        {
            await _notificationService.SendAsync(
                approverEmail,
                "Approval Required",
                $"Workflow instance {instanceId}: {message}",
                "approval",
                null,
                ct);
            _logger.LogInformation("Notification sent to {Email} for workflow instance {InstanceId}",
                approverEmail, instanceId);
            return $"Notification sent to {approverEmail} for workflow instance {instanceId}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify approver {Email}", approverEmail);
            return $"Error sending notification: {ex.Message}";
        }
    }

    [KernelFunction("list_pending_approvals")]
    [Description("List pending approval requests for the current user")]
    [return: Description("List of pending approval requests")]
    public async Task<string> ListPendingApprovalsAsync(CancellationToken ct = default)
    {
        try
        {
            var tenantId = _currentUser.TenantId ?? throw new InvalidOperationException("User tenant not found");
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("User ID not found");

            var pending = await _approvalService.GetPendingForApproverAsync(tenantId, userId, ct);
            return JsonSerializer.Serialize(pending.Select(p => new
            {
                p.Id,
                p.WorkflowName,
                Requester = $"{p.RequesterFirstName} {p.RequesterLastName}",
                p.RequestedAt,
                p.DueAt,
                p.EscalationLevel
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list pending approvals");
            return $"Error: {ex.Message}";
        }
    }
}
