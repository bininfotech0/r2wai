namespace R2WAI.Api.Services;

public class NoOpWorkflowBridge : IWorkflowBridge
{
    public Task<(string ElsaInstanceId, Guid WorkflowInstanceId)> StartWorkflowAsync(
        Guid workflowId, Guid tenantId, Guid userId, string? data, CancellationToken ct)
    {
        return Task.FromResult(("noop", Guid.NewGuid()));
    }

    public Task ResumeWorkflowAsync(string elsaInstanceId, string approvalRequestId, string approvalStatus, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<bool> RetryFailedStepAsync(Guid workflowInstanceId, CancellationToken ct)
    {
        return Task.FromResult(false);
    }
}
