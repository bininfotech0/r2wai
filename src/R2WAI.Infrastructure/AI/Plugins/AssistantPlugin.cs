using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace R2WAI.Infrastructure.AI.Plugins;

public class AssistantPlugin
{
    private readonly IAssistantService _assistantService;
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly ILogger<AssistantPlugin> _logger;

    public AssistantPlugin(
        IAssistantService assistantService,
        IKnowledgeBaseService knowledgeBaseService,
        ILogger<AssistantPlugin> logger)
    {
        _assistantService = assistantService;
        _knowledgeBaseService = knowledgeBaseService;
        _logger = logger;
    }

    [KernelFunction("get_assistant_context")]
    [Description("Get the context and configuration for an assistant")]
    [return: Description("The assistant context information")]
    public async Task<string> GetAssistantContextAsync(
        [Description("The assistant ID")] Guid assistantId,
        CancellationToken ct = default)
    {
        try
        {
            var assistant = await _assistantService.GetAssistantByIdAsync(assistantId, ct);
            return JsonSerializer.Serialize(assistant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assistant context for {AssistantId}", assistantId);
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("get_knowledge_base_context")]
    [Description("Get context from a knowledge base for answering questions")]
    [return: Description("Relevant knowledge base context")]
    public async Task<string> GetKnowledgeBaseContextAsync(
        [Description("The knowledge base ID")] Guid knowledgeBaseId,
        [Description("The query to search for")] string query,
        [Description("Maximum number of results")] int maxResults = 5,
        CancellationToken ct = default)
    {
        try
        {
            var results = await _knowledgeBaseService.SearchKnowledgeBaseAsync(
                knowledgeBaseId, query, 1, maxResults, ct);

            var context = results.Items.Select(r => new
            {
                r.Content,
                r.SourceName,
                r.Score
            });

            return JsonSerializer.Serialize(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get KB context for {KbId}", knowledgeBaseId);
            return $"Error: {ex.Message}";
        }
    }
}
