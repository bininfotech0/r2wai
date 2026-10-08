using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Enums;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// Same windowing and optional summarization as ConversationMemoryService, over ChatbotSessionTurn
/// rows instead of Messages. All queries use IgnoreQueryFilters plus explicit tenant and chatbot
/// predicates: these anonymous requests carry no ambient tenant, so the global filter would match
/// nothing.
/// </summary>
public class ChatbotSessionMemory(
    ApplicationDbContext dbContext,
    IAIService aiService,
    IConfiguration configuration,
    ILogger<ChatbotSessionMemory> logger) : IChatbotSessionMemory
{
    private const int SummarizedFetchWindow = 60;
    private const int SummarizedRecentTurnCount = 10;
    private const int SummaryMaxLength = 800;

    private bool Enabled => configuration.GetValue("Chatbots:SessionMemory:Enabled", true);

    private int MaxTurns => Math.Clamp(configuration.GetValue("Chatbots:SessionMemory:MaxTurns", 20), 2, 200);

    private int RetentionDays => Math.Clamp(configuration.GetValue("Chatbots:SessionMemory:RetentionDays", 7), 1, 365);

    public async Task<string?> BuildHistoryAsync(Chatbot chatbot, string sessionId, CancellationToken ct = default)
    {
        if (!Enabled) return null;

        var summarizationEnabled = configuration.GetValue("AI:ContextMemory:SummarizationEnabled", false);
        var window = summarizationEnabled ? SummarizedFetchWindow : MaxTurns;
        // Rows past retention may not be swept yet; never let them back into a reply.
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

        var turns = await dbContext.ChatbotSessionTurns
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted && t.TenantId == chatbot.TenantId && t.ChatbotId == chatbot.Id
                && t.SessionId == sessionId && t.CreatedAt >= cutoff)
            .OrderByDescending(t => t.CreatedAt)
            .Take(window)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new { t.Role, t.Content })
            .ToListAsync(ct);

        if (turns.Count == 0) return null;

        var pairs = turns.Select(t => (t.Role, t.Content)).ToList();
        if (!summarizationEnabled)
            return ConversationContextBuilder.JoinTurns(pairs);

        var (olderText, recentText) = ConversationContextBuilder.Split(pairs, SummarizedRecentTurnCount);
        if (olderText is null) return recentText;

        string? summary = null;
        try
        {
            summary = await aiService.SummarizeTextAsync(olderText, SummaryMaxLength, ct: ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Failed to summarize older turns for chatbot {ChatbotId} session — using recent turns only", chatbot.Id);
        }

        return ConversationContextBuilder.ComposeWithSummary(summary, recentText);
    }

    public async Task AppendExchangeAsync(Chatbot chatbot, string sessionId, string visitorMessage, string reply, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(reply)) return;

        var now = DateTime.UtcNow;
        var visitorTurn = new ChatbotSessionTurn(
            Guid.NewGuid(), chatbot.TenantId, chatbot.Id, sessionId, MessageRole.User, visitorMessage, now);
        // +1 ms keeps the pair's order unambiguous (Postgres stores microseconds, so a tick is lost).
        var replyTurn = new ChatbotSessionTurn(
            Guid.NewGuid(), chatbot.TenantId, chatbot.Id, sessionId, MessageRole.Assistant, reply, now.AddMilliseconds(1));

        try
        {
            dbContext.ChatbotSessionTurns.AddRange(visitorTurn, replyTurn);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // The visitor already has their reply; losing one remembered turn must not turn that
            // into an error. Detach so a later save in this request doesn't retry the failed rows.
            dbContext.Entry(visitorTurn).State = EntityState.Detached;
            dbContext.Entry(replyTurn).State = EntityState.Detached;
            logger.LogWarning(ex, "Failed to store session memory for chatbot {ChatbotId}", chatbot.Id);
        }
    }
}
