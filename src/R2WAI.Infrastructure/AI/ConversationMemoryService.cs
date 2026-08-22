using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Infrastructure.AI;

public class ConversationMemoryService : IConversationMemoryService
{
    // Matches ChatService's original inline .Take(20) exactly, so the flag-off path is byte-identical
    // to pre-Phase-6 behavior.
    private const int LegacyWindowSize = 20;

    private const int SummarizedFetchWindow = 60;
    private const int SummarizedRecentTurnCount = 10;
    private const int SummaryMaxLength = 800;

    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConversationMemoryService> _logger;

    public ConversationMemoryService(
        ApplicationDbContext context,
        IAIService aiService,
        IConfiguration configuration,
        ILogger<ConversationMemoryService> logger)
    {
        _context = context;
        _aiService = aiService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> BuildConversationContextAsync(Guid conversationId, CancellationToken ct = default)
    {
        var summarizationEnabled = _configuration.GetValue("AI:ContextMemory:SummarizationEnabled", false);
        var windowSize = summarizationEnabled ? SummarizedFetchWindow : LegacyWindowSize;

        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(windowSize)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync(ct);

        var turns = messages.Select(m => (m.Role, m.Content)).ToList();

        if (!summarizationEnabled)
            return ConversationContextBuilder.JoinTurns(turns);

        var (olderText, recentText) = ConversationContextBuilder.Split(turns, SummarizedRecentTurnCount);
        if (olderText is null)
            return recentText;

        string? summary = null;
        try
        {
            summary = await _aiService.SummarizeTextAsync(olderText, SummaryMaxLength, ct);
        }
        catch (Exception ex)
        {
            // Summarization is a quality-of-life improvement over hard truncation, not a
            // correctness requirement — a failed summary call should degrade to "recent turns
            // only" rather than fail the whole chat request.
            _logger.LogWarning(ex, "Failed to summarize older turns for conversation {ConversationId} — falling back to recent turns only", conversationId);
        }

        return ConversationContextBuilder.ComposeWithSummary(summary, recentText);
    }
}
