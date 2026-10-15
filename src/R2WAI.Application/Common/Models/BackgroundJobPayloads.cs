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
    public const string LearnUserMemory = "LearnUserMemory";
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

/// <summary>
/// One finished chat turn for the opt-in memory learning pass. Carries the turn text because the
/// agent test pane's stream path persists no Message rows to re-read; both are the already
/// PII-processed text that reached the model, truncated by the enqueuer. The handler re-checks the
/// user's settings, so switching learning off stops jobs that are already queued.
/// ModelConfigurationId is the model that answered the turn (null: the tenant default), so the pass
/// never sends the turn to a provider the chat itself would not have used.
/// </summary>
public record LearnUserMemoryJobPayload(
    Guid TenantId,
    Guid UserId,
    Guid? AssistantId,
    Guid? ConversationId,
    Guid? ModelConfigurationId,
    string UserMessage,
    string Reply);
