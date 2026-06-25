using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace R2WAI.Infrastructure.AI.Plugins;

public class RAGPlugin
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly ILogger<RAGPlugin> _logger;

    public RAGPlugin(IKnowledgeBaseService knowledgeBaseService, ILogger<RAGPlugin> logger)
    {
        _knowledgeBaseService = knowledgeBaseService;
        _logger = logger;
    }

    [KernelFunction("search_knowledge_base")]
    [Description("Search a knowledge base using semantic search")]
    [return: Description("Search results from the knowledge base")]
    public async Task<string> SearchKnowledgeBaseAsync(
        [Description("The knowledge base ID")] Guid knowledgeBaseId,
        [Description("The search query")] string query,
        [Description("Number of results")] int topN = 5,
        CancellationToken ct = default)
    {
        try
        {
            var results = await _knowledgeBaseService.SearchKnowledgeBaseAsync(
                knowledgeBaseId, query, 1, topN, ct);
            return JsonSerializer.Serialize(results.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search knowledge base {KbId}", knowledgeBaseId);
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("retrieve_documents")]
    [Description("Retrieve documents from a knowledge base")]
    [return: Description("List of documents in the knowledge base")]
    public async Task<string> RetrieveDocumentsAsync(
        [Description("The knowledge base ID")] Guid knowledgeBaseId,
        [Description("Number of documents to retrieve")] int limit = 10,
        CancellationToken ct = default)
    {
        try
        {
            var documents = await _knowledgeBaseService.SearchKnowledgeBaseAsync(
                knowledgeBaseId, string.Empty, 1, limit, ct);
            return JsonSerializer.Serialize(documents.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve documents from KB {KbId}", knowledgeBaseId);
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("get_citations")]
    [Description("Get citations from search results")]
    [return: Description("Citations formatted as text")]
    public async Task<string> GetCitationsAsync(
        [Description("The knowledge base ID")] Guid knowledgeBaseId,
        [Description("The search query")] string query,
        CancellationToken ct = default)
    {
        try
        {
            var results = await _knowledgeBaseService.SearchKnowledgeBaseAsync(
                knowledgeBaseId, query, 1, 5, ct);

            var citations = results.Items.Select((r, i) =>
                $"[{i + 1}] Source: {r.SourceName ?? "Unknown"}, Score: {r.Score:F2}, Content: {r.Content[..Math.Min(r.Content.Length, 200)]}");

            return string.Join("\n\n", citations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get citations from KB {KbId}", knowledgeBaseId);
            return $"Error: {ex.Message}";
        }
    }
}
