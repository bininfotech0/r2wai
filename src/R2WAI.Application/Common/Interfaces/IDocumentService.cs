namespace R2WAI.Application.Common.Interfaces;

public interface IDocumentService
{
    Task<DocumentDto> UploadDocumentAsync(Guid tenantId, Guid userId, string name, string filePath, long fileSize, Guid? knowledgeBaseId, CancellationToken ct = default);
    // expectedTenantId: pass this when the caller has no ambient tenant_id claim (the background
    // IndexDocumentJobHandler path — see ChatbotsController/KnowledgeBaseService's identical pattern
    // for the full reasoning) but already knows, from the event that queued the job, which tenant's
    // document this must be. The authenticated reprocess path (ProcessDocumentCommand) omits it and
    // keeps relying on the ambient filter as its real cross-tenant guard.
    Task ProcessDocumentAsync(Guid documentId, CancellationToken ct = default, Guid? expectedTenantId = null);
    Task DeleteDocumentAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<DocumentDto>> GetDocumentsAsync(Guid tenantId, int page, int pageSize, Guid? knowledgeBaseId, CancellationToken ct = default);
    Task<DocumentDto> GetDocumentByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentSummaryDto> SummarizeDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<ExtractionResultDto> ExtractDocumentAsync(Guid documentId, string schema, CancellationToken ct = default);
    Task<ComparisonResultDto> CompareDocumentsAsync(Guid sourceId, Guid targetId, CancellationToken ct = default);
    Task<string> AskDocumentAsync(Guid documentId, string question, CancellationToken ct = default);
}
