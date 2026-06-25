using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class AssistantDefinition : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public AssistantType Type { get; private set; }
    public string? SystemPrompt { get; private set; }
    public Guid? ModelConfigurationId { get; private set; }
    public Guid? KnowledgeBaseId { get; private set; }
    public string? Tools { get; private set; }
    public string? Settings { get; private set; }
    public bool IsActive { get; private set; }
    public PublishStatus PublishStatus { get; private set; } = PublishStatus.Draft;
    public int PublishedVersion { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public string? Tags { get; private set; }
    public string? AvatarUrl { get; private set; }
    public int UsageCount { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ModelConfiguration? ModelConfiguration { get; private set; }
    public KnowledgeBase? KnowledgeBase { get; private set; }

    private AssistantDefinition() { }

    public AssistantDefinition(Guid id, Guid tenantId, string name, AssistantType type,
                                Guid? modelConfigurationId = null, Guid? knowledgeBaseId = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Type = type;
        ModelConfigurationId = modelConfigurationId;
        KnowledgeBaseId = knowledgeBaseId;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, string? systemPrompt,
                               string? tools, string? settings,
                               string? tags = null, string? avatarUrl = null)
    {
        Name = name;
        Description = description;
        SystemPrompt = systemPrompt;
        Tools = tools;
        Settings = settings;
        if (tags is not null) Tags = tags;
        if (avatarUrl is not null) AvatarUrl = avatarUrl;
        MarkAsModified();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsModified();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsModified();
    }

    public void Publish()
    {
        IsActive = true;
        PublishStatus = PublishStatus.Published;
        PublishedVersion++;
        PublishedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Unpublish()
    {
        IsActive = false;
        PublishStatus = PublishStatus.Draft;
        MarkAsModified();
    }

    public void Archive()
    {
        IsActive = false;
        PublishStatus = PublishStatus.Archived;
        MarkAsModified();
    }

    public void SetAvatarUrl(string? avatarUrl)
    {
        AvatarUrl = avatarUrl;
        MarkAsModified();
    }

    public void IncrementUsageCount()
    {
        UsageCount++;
        MarkAsModified();
    }

    public void LinkModelConfiguration(Guid modelConfigurationId)
    {
        ModelConfigurationId = modelConfigurationId;
        MarkAsModified();
    }

    public void LinkKnowledgeBase(Guid knowledgeBaseId)
    {
        KnowledgeBaseId = knowledgeBaseId;
        MarkAsModified();
    }
}
