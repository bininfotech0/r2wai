namespace R2WAI.Application.Common.Models;

/// <summary>
/// Payload shapes for IBackgroundJobQueue.EnqueueAsync's JobType strings. Kept here (not next to
/// their handlers in Infrastructure) since the enqueue side of each is also Application-layer
/// code (DocumentUploadedEventHandler) or needs a stable contract independent of where the
/// handler happens to live.
/// </summary>
public static class BackgroundJobTypes
{
    public const string NotifyApprovers = "NotifyApprovers";
    public const string IndexDocument = "IndexDocument";
}

/// <summary>
/// Raw IDs only, deliberately -- approvers/requester/workflow name are all re-resolved by the
/// handler at execution time (not enqueue time), matching the original inline-closure behavior
/// this replaced: a rename/role change between enqueue and processing is reflected, not frozen.
/// RequesterId null + EscalationLevel set means "this is an escalation to the next approval
/// level" (no single requester to name); RequesterId set + EscalationLevel null means "this is
/// the initial approval request". WorkflowId is null for a request that was not raised by a workflow
/// step; jobs already queued with a workflow id deserialize unchanged.
/// </summary>
public record NotifyApproversJobPayload(
    Guid ApprovalRequestId,
    Guid TenantId,
    Guid? WorkflowId,
    string[] Roles,
    Guid? RequesterId,
    string? Data,
    int? EscalationLevel);

public record IndexDocumentJobPayload(Guid DocumentId, Guid TenantId);
