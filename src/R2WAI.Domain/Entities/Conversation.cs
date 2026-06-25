using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class Conversation : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; }
    public string? Module { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Metadata { get; private set; }
    public bool IsArchived { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public User User { get; private set; } = null!;
    public ICollection<Message> Messages { get; private set; } = [];

    private Conversation() { }

    public Conversation(Guid id, Guid tenantId, Guid userId, string title,
                        string? module = null, Guid? referenceId = null)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Title = title;
        Module = module;
        ReferenceId = referenceId;
        CreatedAt = DateTime.UtcNow;
    }

    public Message AddMessage(Guid messageId, Guid? parentMessageId,
                               Enums.MessageRole role, string content,
                               string? contentBlocks = null, string? modelUsed = null,
                               int? tokensUsed = null, string? metadata = null)
    {
        var message = new Message(messageId, Id, TenantId, UserId, parentMessageId,
                                   role, content, contentBlocks, modelUsed,
                                   tokensUsed, metadata);
        Messages.Add(message);
        MarkAsModified();
        return message;
    }

    public void Archive()
    {
        IsArchived = true;
        MarkAsModified();
    }

    public void Rename(string title)
    {
        Title = title;
        MarkAsModified();
    }
}
