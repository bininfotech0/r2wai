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

public class ChatbotUsageDto
{
    /// <summary>Lifetime count of replies served across Chat/StreamChat/Webhook — this chatbot's own
    /// "volume", independent of any policy.</summary>
    public long TotalMessagesServed { get; init; }
    /// <summary>Tenant-wide AiUsage policy, for context: this chatbot's requests count against the
    /// same shared daily cap every other chat entry point does. Null cap means no limit configured.</summary>
    public int? TenantDailyCap { get; init; }
    public int TenantDailyUsed { get; init; }
    /// <summary>docs/api/MISSING-BACKEND-ENDPOINTS.md §3.1 #55 — thumbs up/down on a reply, recorded
    /// via POST .../feedback. Not linked to a specific message: no Message rows exist for the
    /// anonymous chat paths (see TotalMessagesServed), so this is a lifetime satisfaction tally,
    /// not per-conversation feedback history.</summary>
    public long PositiveFeedbackCount { get; init; }
    public long NegativeFeedbackCount { get; init; }
}
