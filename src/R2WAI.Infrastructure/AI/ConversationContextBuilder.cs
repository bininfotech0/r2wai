using R2WAI.Domain.Enums;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// Pure formatting/splitting logic behind ConversationMemoryService's short-term/long-term context
/// split. Kept free of EF/AI-service types so the algorithm is directly unit-testable without a
/// database or a live model call.
/// </summary>
public static class ConversationContextBuilder
{
    public static string JoinTurns(IReadOnlyList<(MessageRole Role, string Content)> messages) =>
        string.Join("\n", messages.Select(m => $"{(m.Role == MessageRole.User ? "User" : "Assistant")}: {m.Content}"));

    /// <summary>
    /// Splits into (older, recent) turn text. Older is null when everything fits within
    /// <paramref name="recentTurnCount"/> — nothing needs summarizing.
    /// </summary>
    public static (string? OlderText, string RecentText) Split(
        IReadOnlyList<(MessageRole Role, string Content)> messages, int recentTurnCount)
    {
        if (messages.Count <= recentTurnCount)
            return (null, JoinTurns(messages));

        var splitIndex = messages.Count - recentTurnCount;
        var older = messages.Take(splitIndex).ToList();
        var recent = messages.Skip(splitIndex).ToList();
        return (JoinTurns(older), JoinTurns(recent));
    }

    /// <summary>Null/empty summary collapses to just the recent text (e.g. when summarization failed).</summary>
    public static string ComposeWithSummary(string? summary, string recentText) =>
        string.IsNullOrWhiteSpace(summary)
            ? recentText
            : $"[Earlier conversation summary]\n{summary}\n\n[Recent messages]\n{recentText}";
}
