using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A tenant-editable, versioned override of an AssistantType's base system prompt (see
/// SystemPromptTemplates, the permanent hardcoded fallback used when no active override exists).
/// Editing a template creates a new row rather than mutating one in place, so prior wording is never
/// lost and an in-flight assistant's already-resolved AssistantDefinition.SystemPrompt is unaffected —
/// only newly-created assistants (or ones that explicitly re-pull the type default) pick up an edit.
/// </summary>
public sealed class PromptTemplate : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public AssistantType AssistantType { get; private set; }
    public string Content { get; private set; }
    public int Version { get; private set; }
    public bool IsActive { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private PromptTemplate() { }

    public PromptTemplate(Guid id, Guid tenantId, AssistantType assistantType, string content, int version)
    {
        Id = id;
        TenantId = tenantId;
        AssistantType = assistantType;
        Content = content;
        Version = version;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Superseded by a newer version — kept for history, no longer resolved as "the" active template.</summary>
    public void Supersede()
    {
        IsActive = false;
        MarkAsModified();
    }
}
