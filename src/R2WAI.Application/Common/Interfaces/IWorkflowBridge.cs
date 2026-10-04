namespace R2WAI.Application.Common.Interfaces;

public interface IWorkflowBridge
{
    Task<(string ElsaInstanceId, Guid WorkflowInstanceId)> StartWorkflowAsync(
        Guid workflowId, Guid tenantId, Guid userId, string? data, CancellationToken ct, Guid? existingInstanceId = null);
    Task ResumeWorkflowAsync(string elsaInstanceId, string approvalRequestId, string approvalStatus, CancellationToken ct);
    Task<bool> RetryFailedStepAsync(Guid workflowInstanceId, CancellationToken ct);
    Task<bool> ContinueDelayedWorkflowAsync(Guid workflowInstanceId, CancellationToken ct);
}
