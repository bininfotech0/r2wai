using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// An immutable, versioned snapshot of an <see cref="AssistantDefinition"/>'s configuration.
/// Never mutated or overwritten — rollback always creates a new version rather than editing
/// history. Mirrors <see cref="KnowledgeBaseVersion"/>'s shape exactly.
/// </summary>
public sealed class AssistantVersion : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid AssistantDefinitionId { get; private set; }
    public int VersionNumber { get; private set; }
    public string ConfigSnapshot { get; private set; }
    public bool IsPublished { get; private set; }
    public string? Note { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public AssistantDefinition AssistantDefinition { get; private set; } = null!;

    private AssistantVersion() { }

    private AssistantVersion(Guid id, Guid tenantId, Guid assistantDefinitionId, int versionNumber,
                              string configSnapshot, string? note)
    {
        Id = id;
        TenantId = tenantId;
        AssistantDefinitionId = assistantDefinitionId;
        VersionNumber = versionNumber;
        ConfigSnapshot = configSnapshot;
        Note = note;
        CreatedAt = DateTime.UtcNow;
    }

    public static AssistantVersion CreateSnapshot(Guid id, Guid tenantId, Guid assistantDefinitionId,
                                                    int versionNumber, string configSnapshot, string? note = null) =>
        new(id, tenantId, assistantDefinitionId, versionNumber, configSnapshot, note);

    public void Publish(Guid publishedByUserId)
    {
        IsPublished = true;
        PublishedByUserId = publishedByUserId;
        PublishedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Unpublish()
    {
        IsPublished = false;
        MarkAsModified();
    }
}
