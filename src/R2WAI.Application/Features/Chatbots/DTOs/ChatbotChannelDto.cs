namespace R2WAI.Application.Features.Chatbots.DTOs;

public class ChatbotChannelDto
{
    public ChatbotChannelType ChannelType { get; init; }
    public bool IsConnected { get; init; }
    public DateTime? ConnectedAt { get; init; }
}

public class WebhookKeyInfoDto
{
    public string WebhookUrl { get; init; } = string.Empty;
    public string? ApiKeyPrefix { get; init; }
    public bool HasKey { get; init; }
}

public class RegenerateWebhookKeyResultDto
{
    public string RawKey { get; init; } = string.Empty;
    public string KeyPrefix { get; init; } = string.Empty;
}
