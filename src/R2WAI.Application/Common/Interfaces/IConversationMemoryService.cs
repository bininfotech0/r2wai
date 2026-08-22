namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Builds the conversation-history text passed to the AI for a given conversation (Context &amp;
/// Memory). Behind "AI:ContextMemory:SummarizationEnabled" (default false): off, this reproduces the
/// original fixed-window-and-join behavior exactly; on, older turns beyond the recent window are
/// summarized instead of dropped/hard-truncated by character count downstream.
/// </summary>
public interface IConversationMemoryService
{
    Task<string> BuildConversationContextAsync(Guid conversationId, CancellationToken ct = default);
}
