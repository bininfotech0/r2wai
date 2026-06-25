using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Events;

public sealed class WorkflowCompletedEvent : BaseDomainEvent
{
    public Guid WorkflowInstanceId { get; }
    public Guid WorkflowId { get; }
    public Guid TenantId { get; }
    public WorkflowInstanceStatus Status { get; }

    public WorkflowCompletedEvent(Guid workflowInstanceId, Guid workflowId,
                                   Guid tenantId, WorkflowInstanceStatus status)
    {
        WorkflowInstanceId = workflowInstanceId;
        WorkflowId = workflowId;
        TenantId = tenantId;
        Status = status;
    }
}
