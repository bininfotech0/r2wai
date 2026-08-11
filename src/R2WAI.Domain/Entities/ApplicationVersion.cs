using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// An immutable, versioned snapshot of a <see cref="ConnectedApplication"/>'s configuration
/// (details, API roots, settings). Never mutated or overwritten — rollback always creates a new
/// version rather than editing history.
/// </summary>
public sealed class ApplicationVersion : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public int VersionNumber { get; private set; }
    public string ConfigSnapshot { get; private set; }
    public bool IsPublished { get; private set; }
    public string? Note { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public ConnectedApplication Application { get; private set; } = null!;

    private ApplicationVersion() { }

    private ApplicationVersion(Guid id, Guid tenantId, Guid applicationId, int versionNumber,
                               string configSnapshot, string? note)
    {
        Id = id;
        TenantId = tenantId;
        ApplicationId = applicationId;
        VersionNumber = versionNumber;
        ConfigSnapshot = configSnapshot;
        Note = note;
        CreatedAt = DateTime.UtcNow;
    }

    public static ApplicationVersion CreateSnapshot(Guid id, Guid tenantId, Guid applicationId,
                                                     int versionNumber, string configSnapshot, string? note = null) =>
        new(id, tenantId, applicationId, versionNumber, configSnapshot, note);

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
