using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class Chatbot : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? AssistantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? WelcomeMessage { get; private set; }
    public string? SuggestedQuestions { get; private set; }
    public Guid? ModelConfigurationId { get; private set; }
    public Guid? KnowledgeBaseId { get; private set; }
    public string? PromptTemplate { get; private set; }
    public string? Settings { get; private set; }
    public string? EmbedScript { get; private set; }
    public string? WidgetSettings { get; private set; }
    public bool VoiceEnabled { get; private set; }
    public string? AllowedOrigins { get; private set; }
    public ChatbotStatus Status { get; private set; } = ChatbotStatus.Draft;
    public string? WebhookApiKeyHash { get; private set; }
    public string? WebhookApiKeyPrefix { get; private set; }
    public DateTime? WidgetLastSeenAt { get; private set; }
    public string? WidgetLastSeenOrigin { get; private set; }
    public long TotalMessagesServed { get; private set; }
    public long PositiveFeedbackCount { get; private set; }
    public long NegativeFeedbackCount { get; private set; }

    // Provenance only, set the moment this chatbot is published (UpdateStatus(Active)) from
    // whichever AssistantVersion is IsPublished for the linked Assistant at that instant, if any —
    // never cleared by a later Pause/Draft transition, and never re-derived live. This does NOT
    // pin the chatbot's serving behavior to that version: Chat/StreamChat still resolve the live
    // AssistantDefinition today, same as before this existed. It exists so "what was actually live
    // when this was last published" is an honest, answerable question instead of an unknowable one
    // — closing that serve-time binding (so editing a published assistant can no longer silently
    // change an already-published channel) is a deliberately separate, bigger follow-up.
    public Guid? PublishedAssistantVersionId { get; private set; }
    public int? PublishedAssistantVersionNumber { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public User User { get; private set; } = null!;
    public KnowledgeBase? KnowledgeBase { get; private set; }
    public ModelConfiguration? ModelConfiguration { get; private set; }
    public AssistantDefinition? Assistant { get; private set; }
    public AssistantVersion? PublishedAssistantVersion { get; private set; }

    private Chatbot() { }

    public Chatbot(Guid id, Guid tenantId, Guid userId, string name,
                    Guid? knowledgeBaseId = null, Guid? modelConfigurationId = null)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Name = name;
        KnowledgeBaseId = knowledgeBaseId;
        ModelConfigurationId = modelConfigurationId;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, string? welcomeMessage,
                               string? suggestedQuestions, string? promptTemplate)
    {
        Name = name;
        Description = description;
        WelcomeMessage = welcomeMessage;
        SuggestedQuestions = suggestedQuestions;
        PromptTemplate = promptTemplate;
        MarkAsModified();
    }

    public void UpdateStatus(ChatbotStatus status)
    {
        Status = status;
        MarkAsModified();
    }

    /// <summary>Stamps which AssistantVersion was published for the linked Assistant at the moment
    /// this chatbot itself was published. Caller resolves the version (this entity has no query
    /// access); a null <paramref name="assistantVersionId"/> means the assistant has never had a
    /// version published, which is left unset rather than guessed at.</summary>
    public void RecordPublishedAssistantVersion(Guid? assistantVersionId, int? versionNumber, DateTime publishedAtUtc)
    {
        if (assistantVersionId is null) return;
        PublishedAssistantVersionId = assistantVersionId;
        PublishedAssistantVersionNumber = versionNumber;
        PublishedAt = publishedAtUtc;
    }

    public void LinkKnowledgeBase(Guid knowledgeBaseId)
    {
        KnowledgeBaseId = knowledgeBaseId;
        MarkAsModified();
    }

    public void LinkModelConfiguration(Guid modelConfigurationId)
    {
        ModelConfigurationId = modelConfigurationId;
        MarkAsModified();
    }

    public void SetVoiceEnabled(bool enabled)
    {
        VoiceEnabled = enabled;
        MarkAsModified();
    }

    /// <summary>JSON-encoded string[] of allowed embed origins, same convention as SuggestedQuestions.
    /// Null/empty means unrestricted (the pre-existing default — any origin can embed).</summary>
    public void SetAllowedOrigins(string? allowedOrigins)
    {
        AllowedOrigins = allowedOrigins;
        MarkAsModified();
    }

    public void UpdateSettings(string settings)
    {
        Settings = settings;
        MarkAsModified();
    }

    public void UpdateWidget(string embedScript, string widgetSettings)
    {
        EmbedScript = embedScript;
        WidgetSettings = widgetSettings;
        MarkAsModified();
    }

    public void SetWebhookApiKey(string hash, string prefix)
    {
        WebhookApiKeyHash = hash;
        WebhookApiKeyPrefix = prefix;
        MarkAsModified();
    }

    /// <summary>Records that the public widget actually loaded from a real, allowed browser
    /// origin — the honest signal behind the deployment page's "Installation status". Deliberately
    /// does NOT call MarkAsModified: this fires on every anonymous widget page load (GetPublicInfo),
    /// and ModifiedAt is meant to reflect an admin's own change, not passive traffic.</summary>
    public void RecordWidgetSeen(string origin, DateTime seenAtUtc)
    {
        WidgetLastSeenAt = seenAtUtc;
        WidgetLastSeenOrigin = origin;
    }

    public void AssignAssistant(Guid? assistantId)
    {
        AssistantId = assistantId;
        MarkAsModified();
    }
}
