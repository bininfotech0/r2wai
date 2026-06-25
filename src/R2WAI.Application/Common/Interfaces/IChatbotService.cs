namespace R2WAI.Application.Common.Interfaces;

public interface IChatbotService
{
    Task<ChatbotDto> CreateChatbotAsync(Guid tenantId, Guid userId, string name, Guid? knowledgeBaseId, Guid? modelConfigId, CancellationToken ct = default);
    Task<ChatbotDto> UpdateChatbotAsync(Guid id, string name, string? description, string? welcomeMessage, string? suggestedQuestions, string? promptTemplate, CancellationToken ct = default);
    Task DeleteChatbotAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ChatbotDto>> GetChatbotsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<ChatbotDto> GetChatbotByIdAsync(Guid id, CancellationToken ct = default);
}
