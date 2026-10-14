using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>
/// One row per AI tool call that crossed the governed tool gateway — the queryable record of what
/// the AI actually tried and did, which before this lived only as free-form JSON in AuditLogs.
/// Scoped deliberately to gateway calls (Semantic Kernel, Agent Framework, MCP, and confirmed
/// calls resumed after approval); automation/workflow steps are NOT covered — see
/// docs/architecture/EXECUTION-AND-WORKFLOWS.md for why that half still needs custom Elsa activities.
/// <see cref="IdempotencyKey"/> is populated as <c>"approval:{approvalRequestId}"</c> for the one
/// path that has a stable, caller-independent dedup boundary today — resuming an approved AI tool
/// call. A live AI turn's direct calls don't get one: the model may legitimately call the same tool
/// with the same arguments twice in one conversation, so there is no safe key to derive without a
/// caller-supplied scope. See <c>DeferredToolCallExecutor.TryExecuteAsync</c>'s resume-lookup for
/// how this is used to make it safe to call more than once for the same approval.
/// </summary>
public sealed class ToolExecution : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? ToolDefinitionId { get; private set; }
    public string Plugin { get; private set; } = string.Empty;
    public string Function { get; private set; } = string.Empty;
    public ToolExecutionStatus Status { get; private set; }
    public string? DenialReason { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public long? DurationMs { get; private set; }
    public string? Error { get; private set; }

    private ToolExecution() { }

    private ToolExecution(Guid tenantId, Guid? userId, Guid? toolDefinitionId, string plugin, string function, ToolExecutionStatus status)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        ToolDefinitionId = toolDefinitionId;
        Plugin = plugin;
        Function = function;
        Status = status;
        CreatedAt = DateTime.UtcNow;
    }

    public static ToolExecution Denied(Guid tenantId, Guid? userId, Guid? toolDefinitionId, string plugin, string function, string reason)
    {
        var execution = new ToolExecution(tenantId, userId, toolDefinitionId, plugin, function, ToolExecutionStatus.Denied)
        {
            DenialReason = reason,
        };
        execution.CompletedAt = execution.CreatedAt;
        return execution;
    }

    public static ToolExecution AwaitingApproval(Guid tenantId, Guid userId, Guid toolDefinitionId, string plugin, string function, Guid approvalRequestId) =>
        new(tenantId, userId, toolDefinitionId, plugin, function, ToolExecutionStatus.AwaitingApproval)
        {
            ApprovalRequestId = approvalRequestId,
            IdempotencyKey = ApprovalIdempotencyKey(approvalRequestId),
        };

    public static ToolExecution Prepared(Guid tenantId, Guid? userId, Guid? toolDefinitionId, string plugin, string function, Guid? approvalRequestId = null) =>
        new(tenantId, userId, toolDefinitionId, plugin, function, ToolExecutionStatus.Prepared)
        {
            ApprovalRequestId = approvalRequestId,
            IdempotencyKey = approvalRequestId is { } id ? ApprovalIdempotencyKey(id) : null,
        };

    /// <summary>The one stable, caller-independent dedup boundary this ledger has today — see the
    /// class doc comment on <see cref="IdempotencyKey"/>.</summary>
    public static string ApprovalIdempotencyKey(Guid approvalRequestId) => $"approval:{approvalRequestId}";

    /// <summary>An approved call is about to run: AwaitingApproval → Prepared.</summary>
    public void PrepareAfterApproval()
    {
        EnsureStatus(ToolExecutionStatus.AwaitingApproval);
        Status = ToolExecutionStatus.Prepared;
        MarkAsModified();
    }

    /// <summary>The confirmation was rejected: AwaitingApproval → Denied.</summary>
    public void RejectApproval(string reason)
    {
        EnsureStatus(ToolExecutionStatus.AwaitingApproval);
        Status = ToolExecutionStatus.Denied;
        DenialReason = reason;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Succeed(long durationMs) => Complete(ToolExecutionStatus.Succeeded, durationMs, error: null);

    public void Fail(long durationMs, string error) => Complete(ToolExecutionStatus.Failed, durationMs, error);

    /// <summary>Timed out or cancelled after sending — the outcome at the target is not known.</summary>
    public void MarkUnknown(long durationMs, string reason) => Complete(ToolExecutionStatus.Unknown, durationMs, reason);

    private void Complete(ToolExecutionStatus status, long durationMs, string? error)
    {
        EnsureStatus(ToolExecutionStatus.Prepared);
        Status = status;
        DurationMs = durationMs;
        Error = error;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    private void EnsureStatus(ToolExecutionStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Tool execution {Id} is {Status}, expected {expected}.");
    }
}
