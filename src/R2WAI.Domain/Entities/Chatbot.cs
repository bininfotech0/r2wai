using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class Chatbot : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
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
    public ChatbotStatus Status { get; private set; } = ChatbotStatus.Draft;

    public Tenant Tenant { get; private set; } = null!;
    public User User { get; private set; } = null!;
    public KnowledgeBase? KnowledgeBase { get; private set; }
    public ModelConfiguration? ModelConfiguration { get; private set; }

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

}
