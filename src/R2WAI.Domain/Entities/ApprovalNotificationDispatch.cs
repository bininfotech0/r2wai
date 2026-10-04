using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A durable "this approver was already notified" marker — implementation plan Phase 5's
/// idempotency-key ledger, scoped to the real crash-duplication risk a repository research pass
/// found in <c>NotifyApproversJobHandler</c> (not the originally-planned <c>ToolExecution</c>
/// shape for workflow API-call/email steps, which turned out to need a much bigger change — custom
/// Elsa activities replacing Elsa's own <c>SendHttpRequest</c>/<c>SendEmail</c> — to get real
/// dispatched-but-unresolved protection; see docs/architecture/EXECUTION-AND-WORKFLOWS.md).
///
/// <c>NotifyApproversJobHandler</c> emails/notifies every approver in a loop with no per-recipient
/// tracking; a crash (or a retry after any exception) mid-loop used to re-run the whole handler
/// from the first approver, re-sending to everyone already notified. The unique index on
/// (ApprovalRequestId, EscalationLevel, ApproverId) is what the handler checks before sending and
/// writes immediately after — the actual dedup, not just a log of what happened.
/// </summary>
public sealed class ApprovalNotificationDispatch : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid ApprovalRequestId { get; private set; }

    // Matches NotifyApproversJobPayload.EscalationLevel's own convention (null treated as 0 —
    // "the initial request") flattened to a non-null column: 0 is a real, distinct escalation
    // level here (the initial notification), not "no level recorded".
    public int EscalationLevel { get; private set; }
    public Guid ApproverId { get; private set; }

    private ApprovalNotificationDispatch() { }

    public ApprovalNotificationDispatch(Guid id, Guid tenantId, Guid approvalRequestId, int escalationLevel, Guid approverId)
    {
        Id = id;
        TenantId = tenantId;
        ApprovalRequestId = approvalRequestId;
        EscalationLevel = escalationLevel;
        ApproverId = approverId;
        CreatedAt = DateTime.UtcNow;
    }
}
