namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Reads the tenant's active "Knowledge" GlobalPolicy and exposes its optional structured
/// data-classification ceiling (if the admin configured one) to RAG context retrieval.
/// </summary>
public interface IKnowledgePolicyService
{
    /// <summary>Null when no active policy is configured, or its content doesn't specify one.</summary>
    Task<string?> GetMaxClassificationAsync(Guid tenantId, CancellationToken ct = default);
}
