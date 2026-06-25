namespace R2WAI.Application.Common.Interfaces;

public interface IDocumentService
{
    Task<DocumentDto> UploadDocumentAsync(Guid tenantId, Guid userId, string name, string filePath, long fileSize, Guid? knowledgeBaseId, CancellationToken ct = default);
    Task ProcessDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task DeleteDocumentAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<DocumentDto>> GetDocumentsAsync(Guid tenantId, int page, int pageSize, Guid? knowledgeBaseId, CancellationToken ct = default);
    Task<DocumentDto> GetDocumentByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentSummaryDto> SummarizeDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<ExtractionResultDto> ExtractDocumentAsync(Guid documentId, string schema, CancellationToken ct = default);
    Task<ComparisonResultDto> CompareDocumentsAsync(Guid sourceId, Guid targetId, CancellationToken ct = default);
    Task<string> AskDocumentAsync(Guid documentId, string question, CancellationToken ct = default);
}
