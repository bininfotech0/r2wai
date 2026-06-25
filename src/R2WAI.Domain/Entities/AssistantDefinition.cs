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
                               string? tools, string? settings)
    {
        Name = name;
        Description = description;
        SystemPrompt = systemPrompt;
        Tools = tools;
        Settings = settings;
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
        MarkAsModified();
    }

    public void Unpublish()
    {
        IsActive = false;
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
