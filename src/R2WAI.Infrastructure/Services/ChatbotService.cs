using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace R2WAI.Infrastructure.Services;

public class ChatbotService : IChatbotService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ChatbotService> _logger;

    public ChatbotService(
        ApplicationDbContext context,
        ILogger<ChatbotService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ChatbotDto> CreateChatbotAsync(Guid tenantId, Guid userId, string name, Guid? knowledgeBaseId, Guid? modelConfigId, CancellationToken ct = default)
    {
        var chatbot = new Chatbot(Guid.NewGuid(), tenantId, userId, name, knowledgeBaseId, modelConfigId);
        await _context.Chatbots.AddAsync(chatbot, ct);
        await _context.SaveChangesAsync(ct);

        return MapToDto(chatbot);
    }

    public async Task<ChatbotDto> UpdateChatbotAsync(Guid id, string name, string? description, string? welcomeMessage, string? suggestedQuestions, string? promptTemplate, CancellationToken ct = default)
    {
        var chatbot = await _context.Chatbots
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (chatbot is null)
            throw new NotFoundException(nameof(Chatbot), id);

        chatbot.UpdateDetails(name, description, welcomeMessage, suggestedQuestions, promptTemplate);
        await _context.SaveChangesAsync(ct);

        return MapToDto(chatbot);
    }

    public async Task DeleteChatbotAsync(Guid id, CancellationToken ct = default)
    {
        var chatbot = await _context.Chatbots
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (chatbot is null)
            throw new NotFoundException(nameof(Chatbot), id);

        chatbot.SoftDelete();
        await _context.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<ChatbotDto>> GetChatbotsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Chatbots.Where(c => c.TenantId == tenantId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync(ct);

        return new PagedResult<ChatbotDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ChatbotDto> GetChatbotByIdAsync(Guid id, CancellationToken ct = default)
    {
        var chatbot = await _context.Chatbots
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (chatbot is null)
            throw new NotFoundException(nameof(Chatbot), id);

        return MapToDto(chatbot);
    }

    private static ChatbotDto MapToDto(Chatbot chatbot) => new()
    {
        Id = chatbot.Id,
        Name = chatbot.Name,
        Description = chatbot.Description,
        WelcomeMessage = chatbot.WelcomeMessage,
        SuggestedQuestions = chatbot.SuggestedQuestions,
        ModelConfigurationId = chatbot.ModelConfigurationId,
        KnowledgeBaseId = chatbot.KnowledgeBaseId,
        PromptTemplate = chatbot.PromptTemplate,
        Status = chatbot.Status,
        CreatedAt = chatbot.CreatedAt
    };
}
