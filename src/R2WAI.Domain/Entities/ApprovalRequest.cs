using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A request for a human decision before something proceeds. It is a generic authorisation gate: the
/// workflow links are optional, so an approval can be raised by anything (a workflow step today, a
/// governed capability execution next) without a workflow behind it.
/// </summary>
public sealed class ApprovalRequest : BaseEntity<Guid>
{
    public const int MaxSubjectLength = 300;

    public Guid TenantId { get; private set; }
    public Guid? WorkflowInstanceId { get; private set; }
    public Guid? WorkflowId { get; private set; }

    // What is being decided, in words ("Submit supplier ABC Industries"). Shown wherever the workflow
    // name used to be, since a request with no workflow has no name of its own.
    public string? Subject { get; private set; }
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

    public WorkflowInstance? WorkflowInstance { get; private set; }
    public Workflow? Workflow { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public User Requester { get; private set; } = null!;

    private ApprovalRequest() { }

    public ApprovalRequest(Guid id, Guid tenantId, Guid? workflowInstanceId, Guid? workflowId,
        Guid requesterId, string? data = null, DateTime? dueAt = null,
        int approvalLevel = 0, Guid? parentApprovalId = null, string? subject = null)
    {
        Id = id;
        TenantId = tenantId;
        WorkflowInstanceId = workflowInstanceId;
        WorkflowId = workflowId;
        Subject = subject is { Length: > MaxSubjectLength } ? subject[..MaxSubjectLength] : subject;
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

    // Assigns the request to a role without naming a person (what escalation does). AssignApprover
    // needs a user id, and the escalation sweep used to pass Guid.Empty for "nobody" — which then made
    // the request look assigned to a user that doesn't exist, so no real approver could decide it.
    public void AssignApproverRole(string approverRole)
    {
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

    // Called after Approve() when the request was a deferred dynamic-tool-call (see
    // DeferredToolCallPayload) that has now actually been executed — appends the outcome to Comments
    // rather than overwriting it, so the approver's own decision note (if any) survives alongside it.
    public void RecordDeferredExecutionResult(string result)
    {
        Comments = string.IsNullOrEmpty(Comments) ? $"Executed: {result}" : $"{Comments}\n\nExecuted: {result}";
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
}
