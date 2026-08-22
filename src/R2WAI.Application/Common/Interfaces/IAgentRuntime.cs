namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// The seam the reference architecture's "AI Assistant → AI Agent/Intent" step maps onto: the single
/// entry point for a tool-enabled (autonomous function-calling) AI turn, as opposed to
/// <see cref="IAIService"/>'s plain chat/generation methods which anonymous/non-agentic callers (e.g.
/// the public chatbot widget) use directly and deliberately without tools.
///
/// This composes what Phases 2-6 already wired into the underlying IAIService implementation — the
/// Model Gateway, the Tool/Integration Registry bridge, Policy Engine checks, Prompt Management, and
/// Context &amp; Memory — under one name, rather than requiring every agentic caller to know
/// IAIService's enableTools boolean exists and remember to set it. New agentic integrations (e.g. the
/// Automations natural-language generator) should depend on this, not on IAIService directly.
/// </summary>
public interface IAgentRuntime
{
    Task<string> InvokeAsync(string message, string? conversationHistory = null, string? systemPrompt = null, CancellationToken ct = default);

    IAsyncEnumerable<string> StreamAsync(string message, string? conversationHistory = null, string? systemPrompt = null, CancellationToken ct = default);
}
