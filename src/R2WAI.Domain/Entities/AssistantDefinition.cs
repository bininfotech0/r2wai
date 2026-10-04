using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class AssistantDefinition : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid? ApplicationId { get; private set; }
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
    public ConnectedApplication? Application { get; private set; }

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

    // Called once, right after construction, so a brand-new assistant starts deny-by-default (least
    // privilege) instead of silently getting every tool in the tenant — GetEnabledToolIds' null-means-
    // all fallback exists only to keep already-created assistants working unchanged, not as the
    // intended state for a new one. An admin opts a new assistant into tools via the Tools tab (which
    // calls UpdateDetails), same as before.
    public void DenyAllToolsByDefault()
    {
        Tools = "[]";
        MarkAsModified();
    }

    // Null means the Tools tab was never touched — every caller of this must treat that as "not
    // configured" (attach every available tool), not "configured to zero tools", or every existing
    // assistant that predates this filtering would silently lose all tool access. An explicitly
    // empty "[]" (the admin unchecked every capability) correctly returns an empty, non-null set.
    public IReadOnlyCollection<Guid>? GetEnabledToolIds()
    {
        if (Tools is null) return null;
        try
        {
            var ids = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(Tools);
            return ids ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    // Null on empty/missing/malformed Settings — every knob in AssistantBehaviorSettings is optional,
    // so "not configured" and "explicitly left at default" are indistinguishable here, and both
    // correctly result in no behavior change (callers only act on non-null individual properties).
    public AssistantBehaviorSettings? GetBehaviorSettings()
    {
        if (string.IsNullOrWhiteSpace(Settings)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<AssistantBehaviorSettings>(Settings,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
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

    public void UnlinkModelConfiguration()
    {
        ModelConfigurationId = null;
        MarkAsModified();
    }

    public void LinkKnowledgeBase(Guid knowledgeBaseId)
    {
        KnowledgeBaseId = knowledgeBaseId;
        MarkAsModified();
    }

    public void UnlinkKnowledgeBase()
    {
        KnowledgeBaseId = null;
        MarkAsModified();
    }

    public void AssignApplication(Guid? applicationId)
    {
        ApplicationId = applicationId;
        MarkAsModified();
    }
}
