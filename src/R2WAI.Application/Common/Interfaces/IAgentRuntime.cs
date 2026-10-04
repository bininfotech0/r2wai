namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// <paramref name="EnabledToolIds"/> mirrors <see cref="IAIService.ChatAsync"/>'s own parameter:
/// null means "every tool the tenant has," a non-null (possibly empty) collection scopes it down.
/// <paramref name="CorrelationId"/> lets a caller correlate this call with its own unit of work
/// (e.g. a workflow step); null means "generate one" — either way it comes back on the result so
/// the caller always has it, even when it didn't supply one.
/// </summary>
public sealed record AgentRuntimeRequest(
    string Message,
    string? ConversationHistory = null,
    string? SystemPrompt = null,
    IReadOnlyCollection<Guid>? EnabledToolIds = null,
    Guid? CorrelationId = null);

/// <summary><paramref name="ToolCallsMade"/> is the display name of each tool the runtime actually
/// invoked during this turn (via the shared <see cref="IToolGateway"/> — see
/// <see cref="IChatTraceCollector"/>), in call order. Always present, even when empty, so a caller
/// can show/log what happened without needing runtime-specific introspection.</summary>
public sealed record AgentRuntimeResult(string Text, Guid CorrelationId, IReadOnlyList<string> ToolCallsMade);

/// <summary>
/// The seam the reference architecture's "AI Assistant → AI Agent/Intent" step maps onto: the single
/// entry point for a tool-enabled (autonomous function-calling) AI turn, as opposed to
/// <see cref="IAIService"/>'s plain chat/generation methods which anonymous/non-agentic callers (e.g.
/// the public chatbot widget) use directly and deliberately without tools.
///
/// Runtime-neutral by design (implementation plan Phase 4): <see cref="R2WAI.Infrastructure.AI.AgentRuntime"/>
/// backs this with Semantic Kernel (today's only runtime); a future <c>AgentFrameworkRuntime</c> backs
/// it with Microsoft Agent Framework instead, selected per tenant. Every tool call either backing
/// implementation makes flows through the same <see cref="IToolGateway"/> — this interface's job is
/// to stay a thin enough seam that swapping the runtime underneath changes nothing for the caller.
/// </summary>
public interface IAgentRuntime
{
    Task<AgentRuntimeResult> InvokeAsync(AgentRuntimeRequest request, CancellationToken ct = default);

    IAsyncEnumerable<string> StreamAsync(AgentRuntimeRequest request, CancellationToken ct = default);
}
