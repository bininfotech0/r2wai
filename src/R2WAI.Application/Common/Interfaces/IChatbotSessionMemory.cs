using System.Text.RegularExpressions;
using R2WAI.Domain.Entities;

namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Conversation memory for a deployed chatbot's anonymous channels (embed widget, webhook). The
/// caller supplies an opaque SessionId per visitor conversation. Without one, or with memory
/// switched off (Chatbots:SessionMemory:Enabled), every message is answered stand-alone as before.
/// </summary>
public interface IChatbotSessionMemory
{
    /// <summary>Prior turns of this session as prompt text, or null when there are none.</summary>
    Task<string?> BuildHistoryAsync(Chatbot chatbot, string sessionId, CancellationToken ct = default);

    /// <summary>Stores one visitor message and the reply it got. Never throws: memory is best-effort.</summary>
    Task AppendExchangeAsync(Chatbot chatbot, string sessionId, string visitorMessage, string reply, CancellationToken ct = default);
}

public static partial class ChatbotSessionIds
{
    /// <summary>The anonymous embed-widget routes (chat, chat/stream).</summary>
    public const string WidgetChannel = "widget";

    /// <summary>The webhook route, which requires the chatbot's webhook key.</summary>
    public const string WebhookChannel = "webhook";

    // Caller part only; the "webhook:" prefix Scope adds must still fit the 100-char column.
    public const int MaxLength = 90;

    /// <summary>
    /// The stored session key: the normalized caller id prefixed with its channel, or null when the
    /// id is unusable. The prefix is the security boundary between channels — webhook callers use
    /// guessable thread keys (phone numbers, chat ids), and without it an anonymous widget request
    /// sending that same id would be handed the webhook conversation as context.
    /// </summary>
    public static string? Scope(string channel, string? sessionId) =>
        Normalize(sessionId) is { } id ? $"{channel}:{id}" : null;

    /// <summary>
    /// Returns the trimmed id when usable, else null (no memory for this request). The widget sends
    /// a random UUID; webhook callers may send their own thread key (phone number, chat id...).
    /// </summary>
    public static string? Normalize(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        var trimmed = sessionId.Trim();
        return trimmed.Length <= MaxLength && AllowedPattern().IsMatch(trimmed) ? trimmed : null;
    }

    /// <summary>
    /// Combines session history and knowledge-base excerpts into the single context block the AI
    /// service accepts. History goes first: the AI service trims overlong context from the start,
    /// so the oldest turns are dropped before the knowledge excerpts are.
    /// </summary>
    public static string? ComposeContext(string? history, string? knowledge)
    {
        var hasHistory = !string.IsNullOrWhiteSpace(history);
        var hasKnowledge = !string.IsNullOrWhiteSpace(knowledge);
        if (!hasHistory) return hasKnowledge ? knowledge : null;
        if (!hasKnowledge) return $"[Conversation so far]\n{history}";
        return $"[Conversation so far]\n{history}\n\n[Relevant knowledge]\n{knowledge}";
    }

    [GeneratedRegex(@"^[A-Za-z0-9._:@+\-]+$")]
    private static partial Regex AllowedPattern();
}
