using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace R2WAI.Infrastructure.AI.Plugins;

public class DocumentPlugin
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentPlugin> _logger;

    public DocumentPlugin(IDocumentService documentService, ILogger<DocumentPlugin> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    [KernelFunction("summarize_document")]
    [Description("Summarize a document by its ID")]
    [return: Description("The document summary")]
    public async Task<string> SummarizeDocumentAsync(
        [Description("The document ID")] Guid documentId,
        CancellationToken ct = default)
    {
        try
        {
            var summary = await _documentService.SummarizeDocumentAsync(documentId, ct);
            return JsonSerializer.Serialize(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize document {DocumentId}", documentId);
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("extract_from_document")]
    [Description("Extract structured data from a document using a schema")]
    [return: Description("The extracted data")]
    public async Task<string> ExtractFromDocumentAsync(
        [Description("The document ID")] Guid documentId,
        [Description("The extraction schema")] string schema,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _documentService.ExtractDocumentAsync(documentId, schema, ct);
            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract from document {DocumentId}", documentId);
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("compare_documents")]
    [Description("Compare two documents and return the comparison result")]
    [return: Description("The comparison result")]
    public async Task<string> CompareDocumentsAsync(
        [Description("The source document ID")] Guid sourceId,
        [Description("The target document ID")] Guid targetId,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _documentService.CompareDocumentsAsync(sourceId, targetId, ct);
            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare documents {SourceId} and {TargetId}", sourceId, targetId);
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("ask_document")]
    [Description("Ask a question about a document")]
    [return: Description("The answer to the question")]
    public async Task<string> AskDocumentAsync(
        [Description("The document ID")] Guid documentId,
        [Description("The question to ask")] string question,
        CancellationToken ct = default)
    {
        try
        {
            return await _documentService.AskDocumentAsync(documentId, question, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ask document {DocumentId}", documentId);
            return $"Error: {ex.Message}";
        }
    }
}
