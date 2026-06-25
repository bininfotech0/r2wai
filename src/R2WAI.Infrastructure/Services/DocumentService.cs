using Microsoft.EntityFrameworkCore;
using R2WAI.Infrastructure.VectorStore;

namespace R2WAI.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IStorageService _storageService;
    private readonly IAIService _aiService;
    private readonly ICurrentUserService _currentUserService;
    private readonly FileProcessingService _fileProcessingService;
    private readonly IVectorStoreService _vectorStore;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext context,
        IStorageService storageService,
        IAIService aiService,
        ICurrentUserService currentUserService,
        FileProcessingService fileProcessingService,
        IVectorStoreService vectorStore,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _storageService = storageService;
        _aiService = aiService;
        _currentUserService = currentUserService;
        _fileProcessingService = fileProcessingService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<DocumentDto> UploadDocumentAsync(Guid tenantId, Guid userId, string name, string filePath, long fileSize, Guid? knowledgeBaseId, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(name);
        var documentType = extension?.ToLowerInvariant() switch
        {
            ".pdf" => DocumentType.PDF,
            ".docx" => DocumentType.DOCX,
            ".xlsx" => DocumentType.XLSX,
            ".pptx" => DocumentType.PPTX,
            ".txt" or ".md" or ".csv" => DocumentType.Text,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" => DocumentType.Image,
            _ => DocumentType.Text
        };

        var document = new Document(
            Guid.NewGuid(), tenantId, userId, name, documentType,
            filePath, fileSize, knowledgeBaseId);

        document.AddDomainEvent(new DocumentUploadedEvent(
            document.Id, tenantId, userId, name, documentType, fileSize, knowledgeBaseId));

        await _context.Documents.AddAsync(document, ct);
        await _context.SaveChangesAsync(ct);

        return MapToDto(document);
    }

    public async Task ProcessDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document is null)
            throw new NotFoundException(nameof(Document), documentId);

        document.UpdateStatus(DocumentStatus.Processing);
        await _context.SaveChangesAsync(ct);

        try
        {
            var text = await ExtractTextAsync(document, ct);

            var chunks = ChunkText(text, 1000, 200);

            document.SetPageCount(chunks.Count);
            document.SetMetadata(System.Text.Json.JsonSerializer.Serialize(new { Chunks = chunks.Count, Characters = text.Length }));

            if (document.KnowledgeBaseId.HasValue && chunks.Count > 0)
            {
                try
                {
                    var kb = await _context.KnowledgeBases
                        .FirstOrDefaultAsync(k => k.Id == document.KnowledgeBaseId.Value, ct);

                    if (kb is not null && !string.IsNullOrEmpty(kb.VectorCollectionName))
                    {
                        var embeddings = await _aiService.GenerateEmbeddingsAsync(chunks, ct);
                        var vectors = new List<(Guid Id, float[] Vector, Dictionary<string, object> Payload)>();

                        for (var i = 0; i < chunks.Count; i++)
                        {
                            var embedding = embeddings.ElementAtOrDefault(i);
                            if (embedding is null || embedding.Count == 0) continue;

                            vectors.Add((
                                Guid.NewGuid(),
                                [.. embedding],
                                new Dictionary<string, object>
                                {
                                    ["content"] = chunks[i],
                                    ["source"] = $"doc_{document.Id}",
                                    ["documentId"] = document.Id.ToString(),
                                    ["chunkIndex"] = i
                                }
                            ));
                        }

                        if (vectors.Count > 0)
                        {
                            await _vectorStore.UpsertVectorsAsync(kb.VectorCollectionName, vectors, ct);
                            _logger.LogInformation("Indexed {ChunkCount} chunks from document {DocumentId} into {Collection}",
                                vectors.Count, document.Id, kb.VectorCollectionName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to index document {DocumentId} into Qdrant", document.Id);
                }
            }

            document.UpdateStatus(DocumentStatus.Ready);

            document.AddDomainEvent(new DocumentProcessedEvent(document.Id, document.TenantId, true, null, chunks.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {DocumentId}", documentId);
            document.UpdateStatus(DocumentStatus.Failed, ex.Message);
            document.AddDomainEvent(new DocumentProcessedEvent(document.Id, document.TenantId, false, ex.Message));
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteDocumentAsync(Guid id, CancellationToken ct = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (document is null)
            throw new NotFoundException(nameof(Document), id);

        if (!string.IsNullOrEmpty(document.FilePath))
        {
            try { await _storageService.DeleteFileAsync(document.FilePath, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete file {FilePath}", document.FilePath); }
        }

        document.SoftDelete();
        await _context.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<DocumentDto>> GetDocumentsAsync(Guid tenantId, int page, int pageSize, Guid? knowledgeBaseId, CancellationToken ct = default)
    {
        var query = _context.Documents.Where(d => d.TenantId == tenantId);

        if (knowledgeBaseId.HasValue)
            query = query.Where(d => d.KnowledgeBaseId == knowledgeBaseId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => MapToDto(d))
            .ToListAsync(ct);

        return new PagedResult<DocumentDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<DocumentDto> GetDocumentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (document is null)
            throw new NotFoundException(nameof(Document), id);

        return MapToDto(document);
    }

    public async Task<DocumentSummaryDto> SummarizeDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document is null)
            throw new NotFoundException(nameof(Document), documentId);

        var text = await ReadDocumentTextAsync(document, ct);
        var summary = await _aiService.SummarizeTextAsync(text, 500, ct);

        return new DocumentSummaryDto
        {
            DocumentId = documentId,
            Summary = summary,
            WordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
        };
    }

    public async Task<ExtractionResultDto> ExtractDocumentAsync(Guid documentId, string schema, CancellationToken ct = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document is null)
            throw new NotFoundException(nameof(Document), documentId);

        var text = await ReadDocumentTextAsync(document, ct);
        var extracted = await _aiService.ExtractDataAsync(text, schema, ct);

        return new ExtractionResultDto
        {
            DocumentId = documentId,
            ExtractedData = extracted,
            Schema = schema,
            FieldCount = extracted.Split('\n').Length
        };
    }

    public async Task<ComparisonResultDto> CompareDocumentsAsync(Guid sourceId, Guid targetId, CancellationToken ct = default)
    {
        var sourceDoc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == sourceId, ct);
        var targetDoc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == targetId, ct);

        if (sourceDoc is null) throw new NotFoundException(nameof(Document), sourceId);
        if (targetDoc is null) throw new NotFoundException(nameof(Document), targetId);

        var sourceText = await ReadDocumentTextAsync(sourceDoc, ct);
        var targetText = await ReadDocumentTextAsync(targetDoc, ct);
        var comparison = await _aiService.CompareDocumentsAsync(sourceText, targetText, ct);

        return new ComparisonResultDto
        {
            SourceDocumentId = sourceId,
            TargetDocumentId = targetId,
            Comparison = comparison
        };
    }

    public async Task<string> AskDocumentAsync(Guid documentId, string question, CancellationToken ct = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document is null)
            throw new NotFoundException(nameof(Document), documentId);

        var text = await ReadDocumentTextAsync(document, ct);
        return await _aiService.AnswerQuestionAsync(question, text, ct);
    }

    private async Task<string> ExtractTextAsync(Document document, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{document.Name}");
        
        try
        {
            // Download from storage to temp file
            await using (var fileStream = await _storageService.DownloadFileAsync(document.FilePath, ct))
            await using (var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                await fileStream.CopyToAsync(tempStream, ct);
            }

            return document.FileType switch
            {
                DocumentType.PDF => await _fileProcessingService.ExtractTextFromPdfAsync(tempPath, ct),
                DocumentType.DOCX => await _fileProcessingService.ExtractTextFromDocxAsync(tempPath, ct),
                DocumentType.XLSX => await _fileProcessingService.ExtractTextFromXlsxAsync(tempPath, ct),
                DocumentType.PPTX => await _fileProcessingService.ExtractTextFromPptxAsync(tempPath, ct),
                _ => await File.ReadAllTextAsync(tempPath, ct)
            };
        }
        finally
        {
            if (File.Exists(tempPath))
                try { File.Delete(tempPath); } catch { /* ignore */ }
        }
    }

    private async Task<string> ReadDocumentTextAsync(Document document, CancellationToken ct)
    {
        if (document.Status == DocumentStatus.Ready && !string.IsNullOrEmpty(document.Metadata))
        {
            try
            {
                var meta = System.Text.Json.JsonSerializer.Deserialize<dynamic>(document.Metadata);
                if (meta?.Content != null) return meta.Content;
            }
            catch { /* fallback to extraction */ }
        }

        return await ExtractTextAsync(document, ct);
    }

    private static async Task<string> ReadRawTextAsync(string filePath, CancellationToken ct)
    {
        using var reader = new StreamReader(filePath);
        return await reader.ReadToEndAsync(ct);
    }

    private static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();

        if (string.IsNullOrEmpty(text))
            return chunks;

        if (text.Length <= chunkSize)
        {
            chunks.Add(text);
            return chunks;
        }

        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + chunkSize, text.Length);
            if (end < text.Length)
            {
                var lastSpace = text.LastIndexOf(' ', end, chunkSize);
                if (lastSpace > start)
                    end = lastSpace;
            }

            chunks.Add(text[start..end]);
            start = end - overlap;

            if (start >= text.Length)
                break;
        }

        return chunks;
    }

    private static DocumentDto MapToDto(Document document) => new()
    {
        Id = document.Id,
        Name = document.Name,
        Description = document.Description,
        FileType = document.FileType,
        FileSize = document.FileSize,
        Status = document.Status,
        ProcessingError = document.ProcessingError,
        PageCount = document.PageCount,
        KnowledgeBaseId = document.KnowledgeBaseId,
        CreatedAt = document.CreatedAt
    };
}
