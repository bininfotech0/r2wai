using R2WAI.Application.Features.KnowledgeBases.DTOs;

namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// R2WAI 2.0 §6 — bounded, opt-in retrieval mode. Never replaces
/// <see cref="IKnowledgeBaseService.SearchKnowledgeBaseAsync"/> (still the single search
/// implementation, including its Policy Engine classification ceiling and tenant checks) — this
/// only adds a second, optional pass on top: an evidence-sufficiency check, and if that fails, one
/// query rewrite and one retry. Hard-bounded at <see cref="MaxIterations"/> so this can never become
/// an unbounded loop.
/// </summary>
public interface IAgenticRetrievalOrchestrator
{
    Task<AgenticRetrievalResult> RetrieveAsync(
        Guid knowledgeBaseId,
        string query,
        ResolvedModelConfig? modelConfig,
        CancellationToken ct = default);
}

public record AgenticRetrievalResult(
    List<SearchResultDto> Items,
    bool EvidenceSufficient,
    int IterationsUsed,
    IReadOnlyList<string> QueriesTried);
