using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class ApprovalRequest : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowInstanceId { get; private set; }
    public Guid WorkflowId { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid? ApproverId { get; private set; }
    public string? ApproverRole { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public string? Comments { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public DateTime? DueAt { get; private set; }
    public int EscalationLevel { get; private set; }
    public int ApprovalLevel { get; private set; }
    public Guid? ParentApprovalId { get; private set; }
    public string? Data { get; private set; }
    public string? ElsaBookmarkId { get; private set; }

    public WorkflowInstance WorkflowInstance { get; private set; } = null!;
    public Workflow Workflow { get; private set; } = null!;
    public Tenant Tenant { get; private set; } = null!;
    public User Requester { get; private set; } = null!;

    private ApprovalRequest() { }

    public ApprovalRequest(Guid id, Guid tenantId, Guid workflowInstanceId, Guid workflowId,
        Guid requesterId, string? data = null, DateTime? dueAt = null,
        int approvalLevel = 0, Guid? parentApprovalId = null)
    {
        Id = id;
        TenantId = tenantId;
        WorkflowInstanceId = workflowInstanceId;
        WorkflowId = workflowId;
        RequesterId = requesterId;
        Data = data;
        DueAt = dueAt;
        ApprovalLevel = approvalLevel;
        ParentApprovalId = parentApprovalId;
        Status = ApprovalStatus.Pending;
        RequestedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    public void AssignApprover(Guid approverId, string? approverRole = null)
    {
        ApproverId = approverId;
        ApproverRole = approverRole;
        MarkAsModified();
    }

    public void Approve(string? comments = null)
    {
        Status = ApprovalStatus.Approved;
        Comments = comments;
        RespondedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Reject(string? comments = null)
    {
        Status = ApprovalStatus.Rejected;
        Comments = comments;
        RespondedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Escalate()
    {
        Status = ApprovalStatus.Escalated;
        EscalationLevel++;
        MarkAsModified();
    }

    public void Cancel()
    {
        Status = ApprovalStatus.Cancelled;
        RespondedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void SetElsaBookmarkId(string bookmarkId)
    {
        ElsaBookmarkId = bookmarkId;
        MarkAsModified();
    }
}
