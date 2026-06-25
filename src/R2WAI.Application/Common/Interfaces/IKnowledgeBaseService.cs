namespace R2WAI.Application.Common.Interfaces;

public interface IKnowledgeBaseService
{
    Task<KnowledgeBaseDto> CreateKnowledgeBaseAsync(Guid tenantId, Guid userId, string name, string? description, CancellationToken ct = default);
    Task<KnowledgeBaseDto> UpdateKnowledgeBaseAsync(Guid id, string name, string? description, CancellationToken ct = default);
    Task DeleteKnowledgeBaseAsync(Guid id, CancellationToken ct = default);
    Task<KnowledgeBaseSourceDto> AddSourceAsync(Guid knowledgeBaseId, string type, Guid? referenceId, string? url, string? content, CancellationToken ct = default);
    Task RemoveSourceAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<SearchResultDto>> SearchKnowledgeBaseAsync(Guid knowledgeBaseId, string query, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<KnowledgeBaseDto>> GetKnowledgeBasesAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<KnowledgeBaseDto> GetKnowledgeBaseByIdAsync(Guid id, CancellationToken ct = default);
}
