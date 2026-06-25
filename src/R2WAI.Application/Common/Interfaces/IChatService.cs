namespace R2WAI.Application.Common.Interfaces;

public interface IChatService
{
    Task<PagedResult<ConversationDto>> GetConversationsAsync(Guid tenantId, Guid userId, int page, int pageSize, string? module, CancellationToken ct = default);
    Task<ConversationDto> CreateConversationAsync(Guid tenantId, Guid userId, string title, string? module, Guid? referenceId, CancellationToken ct = default);
    Task<ConversationDto> GetConversationByIdAsync(Guid id, CancellationToken ct = default);
    Task DeleteConversationAsync(Guid id, CancellationToken ct = default);
    Task<MessageDto> SendMessageAsync(Guid conversationId, Guid tenantId, Guid userId, string content, IReadOnlyList<MessageAttachmentDto>? attachments, CancellationToken ct = default);
    Task<PagedResult<MessageDto>> GetMessagesAsync(Guid conversationId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<SuggestedActionDto>> GetSuggestedActionsAsync(Guid? conversationId, CancellationToken ct = default);
}
