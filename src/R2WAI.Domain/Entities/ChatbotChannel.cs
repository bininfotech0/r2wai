using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class ChatbotChannel : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid ChatbotId { get; private set; }
    public ChatbotChannelType ChannelType { get; private set; }
    public bool IsConnected { get; private set; }
    public string? EncryptedCredentials { get; private set; }
    public DateTime? ConnectedAt { get; private set; }

    public Chatbot Chatbot { get; private set; } = null!;

    private ChatbotChannel() { }

    public ChatbotChannel(Guid id, Guid tenantId, Guid chatbotId, ChatbotChannelType channelType)
    {
        Id = id;
        TenantId = tenantId;
        ChatbotId = chatbotId;
        ChannelType = channelType;
        CreatedAt = DateTime.UtcNow;
    }

    public void Connect(string encryptedCredentials)
    {
        EncryptedCredentials = encryptedCredentials;
        IsConnected = true;
        ConnectedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Disconnect()
    {
        IsConnected = false;
        MarkAsModified();
    }
}
