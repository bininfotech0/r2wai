using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>
/// One message in an anonymous chatbot session (embed widget or webhook channel). Deliberately not
/// a Conversation/Message: those require a real User, and widget visitors have none. The session
/// is identified only by the caller-supplied SessionId, scoped to one chatbot. Short-lived by
/// design — DataRetentionService hard-deletes rows past Chatbots:SessionMemory:RetentionDays.
/// </summary>
public sealed class ChatbotSessionTurn : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid ChatbotId { get; private set; }
    public string SessionId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; }

    public Chatbot Chatbot { get; private set; } = null!;

    private ChatbotSessionTurn()
    {
        SessionId = string.Empty;
        Content = string.Empty;
    }

    public ChatbotSessionTurn(Guid id, Guid tenantId, Guid chatbotId, string sessionId,
                              MessageRole role, string content, DateTime createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ChatbotId = chatbotId;
        SessionId = sessionId;
        Role = role;
        Content = content;
        CreatedAt = createdAt;
    }
}
