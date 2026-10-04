using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// An immutable, versioned snapshot of a <see cref="KnowledgeBase"/>'s configuration (details,
/// embedding settings, sources). Never mutated or overwritten — rollback always creates a new
/// version rather than editing history. Mirrors <see cref="ApplicationVersion"/>'s shape exactly.
/// </summary>
public sealed class KnowledgeBaseVersion : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid KnowledgeBaseId { get; private set; }
    public int VersionNumber { get; private set; }
    public string ConfigSnapshot { get; private set; }
    public bool IsPublished { get; private set; }
    public string? Note { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public KnowledgeBase KnowledgeBase { get; private set; } = null!;

    private KnowledgeBaseVersion() { }

    private KnowledgeBaseVersion(Guid id, Guid tenantId, Guid knowledgeBaseId, int versionNumber,
                                  string configSnapshot, string? note)
    {
        Id = id;
        TenantId = tenantId;
        KnowledgeBaseId = knowledgeBaseId;
        VersionNumber = versionNumber;
        ConfigSnapshot = configSnapshot;
        Note = note;
        CreatedAt = DateTime.UtcNow;
    }

    public static KnowledgeBaseVersion CreateSnapshot(Guid id, Guid tenantId, Guid knowledgeBaseId,
                                                        int versionNumber, string configSnapshot, string? note = null) =>
        new(id, tenantId, knowledgeBaseId, versionNumber, configSnapshot, note);

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
