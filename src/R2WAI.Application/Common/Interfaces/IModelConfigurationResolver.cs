namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Plain data — deliberately has no OpenAI/Semantic Kernel dependency so it can be referenced from
/// the Application layer (returned by <see cref="IModelConfigurationResolver"/>) and consumed by
/// Infrastructure's <c>IModelGateway</c>/<c>IModelProvider</c> without a layering violation.
/// </summary>
public record ResolvedModelConfig(
    string Provider,
    string? ApiKey,
    string? ModelId,
    string? Endpoint,
    int? MaxTokens,
    double? Temperature,
    double? TopP);

/// <summary>
/// Resolves a tenant-scoped <c>ModelConfiguration</c> row (e.g. an assistant's linked model) into a
/// <see cref="ResolvedModelConfig"/> the AI runtime can actually use — decrypting its API key,
/// verifying it belongs to the calling tenant and is active, and re-checking the data-classification
/// boundary at resolve time (not just at write time). Returns null on any resolution failure
/// (missing/inactive/wrong-tenant/undecryptable/boundary-violated) rather than throwing, so a bad
/// or stale link never breaks an existing chat — callers fall back to the global "AI:Provider"
/// configuration, byte-identical to the pre-existing behavior.
/// </summary>
public interface IModelConfigurationResolver
{
    Task<ResolvedModelConfig?> ResolveAsync(Guid? modelConfigurationId, Guid tenantId, CancellationToken ct = default);
}
