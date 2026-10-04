using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Events;

public sealed class MessageCreatedEvent : BaseDomainEvent
{
    public Guid MessageId { get; }
    public Guid ConversationId { get; }
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public string Content { get; }
    public MessageRole Role { get; }

    public MessageCreatedEvent(Guid messageId, Guid conversationId,
                                Guid tenantId, Guid userId, string content, MessageRole role)
    {
        MessageId = messageId;
        ConversationId = conversationId;
        TenantId = tenantId;
        UserId = userId;
        Content = content;
        Role = role;
    }
}
