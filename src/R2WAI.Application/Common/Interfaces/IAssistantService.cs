namespace R2WAI.Application.Common.Interfaces;

public interface IAssistantService
{
    Task<AssistantDto> CreateAssistantAsync(Guid tenantId, string name, AssistantType type, Guid? modelConfigId, Guid? knowledgeBaseId, CancellationToken ct = default);
    Task<AssistantDto> UpdateAssistantAsync(Guid id, string name, string? description, string? systemPrompt, string? tools, string? settings, CancellationToken ct = default);
    Task DeleteAssistantAsync(Guid id, CancellationToken ct = default);
    Task<string> ChatWithAssistantAsync(Guid id, string message, string? conversationId, CancellationToken ct = default);
    Task<PagedResult<AssistantDto>> GetAssistantsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<AssistantDto> GetAssistantByIdAsync(Guid id, CancellationToken ct = default);
}
