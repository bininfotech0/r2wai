using Microsoft.Extensions.Logging;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.KnowledgeBases.DTOs;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// Real implementation of the bounded Agentic RAG pass described in
/// <see cref="IAgenticRetrievalOrchestrator"/>. Reuses <see cref="IKnowledgeBaseService"/> for every
/// actual search — this class only decides whether one more (rewritten) search is worth running.
/// </summary>
public class AgenticRetrievalOrchestrator(
    IKnowledgeBaseService knowledgeBaseService,
    IAIService aiService,
    ILogger<AgenticRetrievalOrchestrator> logger) : IAgenticRetrievalOrchestrator
{
    // Hard bound: first pass + at most one rewritten retry. Never grows without a code change —
    // this is the actual anti-unbounded-loop guarantee the brief asks for, not a config default
    // someone could turn into "unlimited".
    private const int MaxIterations = 2;
    private const int ResultsPerSearch = 5;

    // This comment used to claim "HybridSearchAsync already filters below 0.7f — every row this
    // receives already cleared that bar". That was wrong: the 0.7f passed to HybridSearchAsync is the
    // vector-vs-full-text blend *weight*, not a relevance floor, and the SQL had no WHERE clause at
    // all, so rows arrived unfiltered. The relevance floor is a separate parameter, now fed from
    // ApplicationConfiguration.RagThreshold via KnowledgeBaseService, and the default there is still
    // 0.7 — so "results arrive with ~0.7 applied" is now true, but for a completely different reason
    // than the one this comment gave. "Sufficient" asks a materially higher bar on top of it: a
    // confident top match, not just any match.
    private const double SufficientScoreThreshold = 0.80;
    private const int MinSufficientResults = 1;

    public async Task<AgenticRetrievalResult> RetrieveAsync(
        Guid knowledgeBaseId, string query, ResolvedModelConfig? modelConfig, CancellationToken ct = default)
    {
        var queriesTried = new List<string> { query };
        var currentQuery = query;
        List<SearchResultDto> items = [];
        var iteration = 0;

        while (true)
        {
            iteration++;
            var page = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                knowledgeBaseId, currentQuery, 1, ResultsPerSearch, ct);

            // Union by Id, keep the higher score seen for a chunk retrieved by both passes.
            foreach (var item in page.Items)
            {
                var existing = items.FirstOrDefault(i => i.Id == item.Id);
                if (existing is null) items.Add(item);
                else if (item.Score > existing.Score) { items.Remove(existing); items.Add(item); }
            }

            var sufficient = items.Count(i => i.Score >= SufficientScoreThreshold) >= MinSufficientResults;
            if (sufficient || iteration >= MaxIterations)
                return new AgenticRetrievalResult(items, sufficient, iteration, queriesTried);

            var rewritten = await TryRewriteQueryAsync(query, currentQuery, modelConfig, ct);
            if (rewritten is null)
                return new AgenticRetrievalResult(items, sufficient, iteration, queriesTried);

            currentQuery = rewritten;
            queriesTried.Add(currentQuery);
        }
    }

    /// <summary>Null return means "don't bother retrying" — a blank/refused/unchanged rewrite would
    /// just spend the one retry budget on a duplicate of the query that already came up short.</summary>
    private async Task<string?> TryRewriteQueryAsync(
        string originalQuery, string currentQuery, ResolvedModelConfig? modelConfig, CancellationToken ct)
    {
        try
        {
            var rewritten = await aiService.GenerateResponseAsync(
                originalQuery,
                systemPrompt: "Rewrite the user's question as a short, specific search query for a document " +
                              "retrieval system. Use different, more specific wording than the original — " +
                              "synonyms, expanded acronyms, or a narrower phrasing. Reply with ONLY the rewritten " +
                              "query text, nothing else.",
                context: null,
                modelConfig: modelConfig,
                maxTokens: 60,
                temperature: 0.3,
                ct: ct);

            var trimmed = rewritten?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(trimmed)
                || trimmed.Equals(currentQuery, StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals(originalQuery, StringComparison.OrdinalIgnoreCase))
                return null;

            return trimmed;
        }
        catch (Exception ex)
        {
            // A failed rewrite call must not fail the whole chat turn — the caller already has
            // whatever the first pass found; this is a best-effort improvement, not a requirement.
            logger.LogWarning(ex, "Agentic RAG query rewrite failed, keeping first-pass results only");
            return null;
        }
    }
}
