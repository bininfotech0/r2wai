using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class Message : BaseEntity<Guid>
{
    public Guid ConversationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ParentMessageId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; }
    public string? ContentBlocks { get; private set; }
    public MessageStatus Status { get; private set; } = MessageStatus.Sending;
    public int? TokensUsed { get; private set; }
    public string? ModelUsed { get; private set; }
    public string? Metadata { get; private set; }

    public Conversation Conversation { get; private set; } = null!;
    public User User { get; private set; } = null!;
    public ICollection<MessageAttachment> Attachments { get; private set; } = [];

    private Message() { }

    public Message(Guid id, Guid conversationId, Guid tenantId, Guid userId,
                   Guid? parentMessageId, MessageRole role, string content,
                   string? contentBlocks = null, string? modelUsed = null,
                   int? tokensUsed = null, string? metadata = null)
    {
        Id = id;
        ConversationId = conversationId;
        TenantId = tenantId;
        UserId = userId;
        ParentMessageId = parentMessageId;
        Role = role;
        Content = content;
        ContentBlocks = contentBlocks;
        ModelUsed = modelUsed;
        TokensUsed = tokensUsed;
        Metadata = metadata;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(MessageStatus status)
    {
        Status = status;
        MarkAsModified();
    }

    public void SetTokensUsed(int tokens)
    {
        TokensUsed = tokens;
        MarkAsModified();
    }

    public void AddAttachment(MessageAttachment attachment)
    {
        Attachments.Add(attachment);
        MarkAsModified();
    }
}
