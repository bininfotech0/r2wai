using Microsoft.EntityFrameworkCore;

namespace R2WAI.Infrastructure.Services;

public class AssistantService : IAssistantService
{
    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        ApplicationDbContext context,
        IAIService aiService,
        IKnowledgeBaseService knowledgeBaseService,
        ILogger<AssistantService> logger)
    {
        _context = context;
        _aiService = aiService;
        _knowledgeBaseService = knowledgeBaseService;
        _logger = logger;
    }

    public async Task<AssistantDto> CreateAssistantAsync(Guid tenantId, string name, AssistantType type, Guid? modelConfigId, Guid? knowledgeBaseId, CancellationToken ct = default)
    {
        var userId = Guid.Empty;
        var assistantsCount = await _context.AssistantDefinitions
            .Where(a => a.TenantId == tenantId)
            .CountAsync(ct);
        if (assistantsCount == 0)
        {
            var firstUser = await _context.Users
                .Where(u => u.TenantId == tenantId)
                .FirstOrDefaultAsync(ct);
            if (firstUser is not null)
                userId = firstUser.Id;
        }

        var assistant = new AssistantDefinition(
            Guid.NewGuid(), tenantId, name, type, modelConfigId, knowledgeBaseId);

        await _context.AssistantDefinitions.AddAsync(assistant, ct);
        await _context.SaveChangesAsync(ct);

        return MapToDto(assistant);
    }

    public async Task<AssistantDto> UpdateAssistantAsync(Guid id, string name, string? description, string? systemPrompt, string? tools, string? settings, CancellationToken ct = default)
    {
        var assistant = await _context.AssistantDefinitions
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assistant is null)
            throw new NotFoundException(nameof(AssistantDefinition), id);

        assistant.UpdateDetails(name, description, systemPrompt, tools, settings);
        await _context.SaveChangesAsync(ct);

        return MapToDto(assistant);
    }

    public async Task DeleteAssistantAsync(Guid id, CancellationToken ct = default)
    {
        var assistant = await _context.AssistantDefinitions
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assistant is null)
            throw new NotFoundException(nameof(AssistantDefinition), id);

        assistant.SoftDelete();
        await _context.SaveChangesAsync(ct);
    }

    public async Task<string> ChatWithAssistantAsync(Guid id, string message, string? conversationId, CancellationToken ct = default)
    {
        var assistant = await _context.AssistantDefinitions
            .Include(a => a.KnowledgeBase)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assistant is null)
            throw new NotFoundException(nameof(AssistantDefinition), id);

        var systemPrompt = assistant.SystemPrompt
            ?? R2WAI.Infrastructure.AI.Prompts.SystemPromptTemplates.GetTemplate(assistant.Type);

        var contextParts = new List<string>();

        if (!string.IsNullOrEmpty(conversationId) && Guid.TryParse(conversationId, out var convId))
        {
            var recentMessages = await _context.Messages
                .Where(m => m.ConversationId == convId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(10)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new { m.Role, m.Content })
                .ToListAsync(ct);

            if (recentMessages.Count > 0)
            {
                var historyText = string.Join("\n", recentMessages.Select(m =>
                    $"{m.Role}: {m.Content}"));
                contextParts.Add($"[Conversation History]\n{historyText}");
            }
        }

        if (assistant.KnowledgeBase is not null)
        {
            var searchResults = await _knowledgeBaseService.SearchKnowledgeBaseAsync(
                assistant.KnowledgeBase.Id, message, 1, 5, ct);
            if (searchResults.Items.Count > 0)
            {
                var kbContext = string.Join("\n\n", searchResults.Items.Select((r, i) =>
                    $"[Source {i + 1}: {r.SourceName ?? "Unknown"}]\n{r.Content}"));
                contextParts.Add($"[Knowledge Base - cite sources when using this information]\n{kbContext}");
            }
        }

        var context = string.Join("\n\n", contextParts);
        return await _aiService.ChatAsync(message, context, systemPrompt, ct);
    }

    public async Task<PagedResult<AssistantDto>> GetAssistantsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.AssistantDefinitions.Where(a => a.TenantId == tenantId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync(ct);

        return new PagedResult<AssistantDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AssistantDto> GetAssistantByIdAsync(Guid id, CancellationToken ct = default)
    {
        var assistant = await _context.AssistantDefinitions
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assistant is null)
            throw new NotFoundException(nameof(AssistantDefinition), id);

        return MapToDto(assistant);
    }

    private static AssistantDto MapToDto(AssistantDefinition assistant) => new()
    {
        Id = assistant.Id,
        Name = assistant.Name,
        Description = assistant.Description,
        Type = assistant.Type,
        SystemPrompt = assistant.SystemPrompt,
        ModelConfigurationId = assistant.ModelConfigurationId,
        KnowledgeBaseId = assistant.KnowledgeBaseId,
        Tools = assistant.Tools,
        Settings = assistant.Settings,
        IsActive = assistant.IsActive,
        CreatedAt = assistant.CreatedAt
    };
}
