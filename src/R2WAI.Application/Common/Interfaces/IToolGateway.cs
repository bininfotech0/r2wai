namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Everything the governance decision needs about one tool call, deliberately independent of any
/// specific AI runtime (Semantic Kernel today; MCP/Agent Framework are meant to build the same
/// request shape from their own call sites — implementation plan Phase 2/3/4). <paramref
/// name="ToolDefinitionId"/> is the caller's own exact-match hint (e.g. Semantic Kernel function
/// metadata) when it has one; null means "resolve by name" is the gateway's job, not the caller's.
/// <paramref name="ExecuteAsync"/> is the actual dispatch — the gateway calls it only after Allow,
/// wraps it for timing/audit/trace, and lets any exception propagate to the caller unchanged.
/// </summary>
public record ToolInvocationRequest(
    string Plugin,
    string Function,
    Guid? ToolDefinitionId,
    string? ArgumentsForAudit,
    string? InputArgument,
    Func<CancellationToken, Task> ExecuteAsync);

/// <summary><paramref name="DenialMessage"/> is set only when <paramref name="Allowed"/> is false —
/// the caller's own user-facing text for why the call didn't run.</summary>
public record ToolInvocationOutcome(bool Allowed, string? DenialMessage);

/// <summary>
/// The single governed execution boundary every tool call must cross: identity/tenant resolution,
/// role/risk/approval policy, the calling assistant's enabled-tool defense-in-depth check, audit,
/// and metrics. <see cref="R2WAI.Infrastructure.AI.AiFunctionAuditFilter"/> is the Semantic Kernel
/// adapter that builds a request from a live function-invocation context and calls this.
/// </summary>
public interface IToolGateway
{
    Task<ToolInvocationOutcome> InvokeAsync(ToolInvocationRequest request, CancellationToken ct = default);
}
