namespace R2WAI.Application.Common.Interfaces;

public interface IWorkflowService
{
    Task<WorkflowDto> CreateWorkflowAsync(Guid tenantId, Guid userId, string name, string? description, string? type, CancellationToken ct = default);
    Task<WorkflowDto> UpdateWorkflowAsync(Guid id, string name, string? description, string? type, string? trigger, string? steps, CancellationToken ct = default);
    Task DeleteWorkflowAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowInstanceDto> ExecuteWorkflowAsync(Guid workflowId, Guid initiatedBy, string? data, CancellationToken ct = default);
    Task<PagedResult<WorkflowDto>> GetWorkflowsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<WorkflowDto> GetWorkflowByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<WorkflowInstanceDto>> GetWorkflowInstancesAsync(Guid tenantId, Guid? workflowId, int page, int pageSize, CancellationToken ct = default);
    Task<WorkflowInstanceDto> GetWorkflowInstanceByIdAsync(Guid id, CancellationToken ct = default);
}
