using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class WorkflowInstance : BaseEntity<Guid>
{
    public Guid WorkflowId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InitiatedBy { get; private set; }
    public WorkflowInstanceStatus Status { get; private set; } = WorkflowInstanceStatus.Running;
    public int CurrentStep { get; private set; }
    public string? Data { get; private set; }
    public string? ElsaInstanceId { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public Workflow Workflow { get; private set; } = null!;
    public Tenant Tenant { get; private set; } = null!;
    public User Initiator { get; private set; } = null!;

    private WorkflowInstance() { }

    public WorkflowInstance(Guid id, Guid workflowId, Guid tenantId, Guid initiatedBy,
                             string? data = null)
    {
        Id = id;
        WorkflowId = workflowId;
        TenantId = tenantId;
        InitiatedBy = initiatedBy;
        Data = data;
        StartedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    public void AdvanceStep()
    {
        CurrentStep++;
        MarkAsModified();
    }

    public void Complete()
    {
        Status = WorkflowInstanceStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Fail()
    {
        Status = WorkflowInstanceStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Cancel()
    {
        Status = WorkflowInstanceStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void SetElsaInstanceId(string elsaInstanceId)
    {
        ElsaInstanceId = elsaInstanceId;
        MarkAsModified();
    }

    public void UpdateData(string data)
    {
        Data = data;
        MarkAsModified();
    }
}
