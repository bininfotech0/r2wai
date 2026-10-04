namespace R2WAI.Application.Common.Interfaces;

public interface IKnowledgeBaseService
{
    Task<KnowledgeBaseDto> CreateKnowledgeBaseAsync(Guid tenantId, Guid userId, string name, string? description, string dataClassification = "Internal", CancellationToken ct = default);
    Task<KnowledgeBaseDto> UpdateKnowledgeBaseAsync(Guid id, string name, string? description, CancellationToken ct = default);
    Task DeleteKnowledgeBaseAsync(Guid id, CancellationToken ct = default);
    Task<KnowledgeBaseSourceDto> AddSourceAsync(Guid knowledgeBaseId, string type, Guid? referenceId, string? url, string? content, CancellationToken ct = default);
    Task RemoveSourceAsync(Guid id, CancellationToken ct = default);
    // expectedTenantId: pass this when the caller has no ambient tenant_id claim to filter by (e.g.
    // an [AllowAnonymous] public chatbot widget) but already knows, from its own tenant-filtered
    // lookup, which tenant's KB this must be — see ChatbotsController.SearchKnowledgeBaseContextAsync.
    // Every existing authenticated caller omits it (trailing optional param) and gets byte-identical
    // behavior (ambient filter).
    Task<PagedResult<SearchResultDto>> SearchKnowledgeBaseAsync(Guid knowledgeBaseId, string query, int page, int pageSize, CancellationToken ct = default, Guid? expectedTenantId = null);
    Task<PagedResult<KnowledgeBaseDto>> GetKnowledgeBasesAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<KnowledgeBaseDto> GetKnowledgeBaseByIdAsync(Guid id, CancellationToken ct = default);
}
