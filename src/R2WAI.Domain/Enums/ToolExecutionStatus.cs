namespace R2WAI.Domain.Enums;

/// <summary>
/// Lifecycle of one AI tool call — the status vocabulary of the execution model in
/// docs/architecture/EXECUTION-AND-WORKFLOWS.md (Decide → Approve → Prepare → Record).
/// </summary>
public enum ToolExecutionStatus
{
    /// <summary>The gateway refused the call (role, risk ceiling, unknown tool, not enabled, or a rejected confirmation).</summary>
    Denied,

    /// <summary>Paused for a human confirmation; <see cref="Entities.ToolExecution.ApprovalRequestId"/> links it.</summary>
    AwaitingApproval,

    /// <summary>Allowed and about to be sent — a row left here means the process died mid-call.</summary>
    Prepared,

    Succeeded,
    Failed,

    /// <summary>Timed out or was cancelled after sending — the target may or may not have acted.</summary>
    Unknown,
}
