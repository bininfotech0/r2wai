using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// An immutable, versioned snapshot of a <see cref="Workflow"/>'s content (name, description, type,
/// trigger, steps) at a given <see cref="Workflow.Version"/> number. Workflow.Steps itself stays a
/// plain mutable "current content" field, overwritten in place on every edit — this table is what
/// preserves what each version actually contained once the row moves on to the next one, and what
/// WorkflowInstance.WorkflowVersionNumber resolves against.
/// </summary>
public sealed class WorkflowVersion : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowId { get; private set; }
    public int VersionNumber { get; private set; }
    public string ConfigSnapshot { get; private set; }
    public bool IsPublished { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public Workflow Workflow { get; private set; } = null!;

    private WorkflowVersion() { }

    private WorkflowVersion(Guid id, Guid tenantId, Guid workflowId, int versionNumber, string configSnapshot)
    {
        Id = id;
        TenantId = tenantId;
        WorkflowId = workflowId;
        VersionNumber = versionNumber;
        ConfigSnapshot = configSnapshot;
        CreatedAt = DateTime.UtcNow;
    }

    public static WorkflowVersion CreateSnapshot(Guid id, Guid tenantId, Guid workflowId,
                                                  int versionNumber, string configSnapshot) =>
        new(id, tenantId, workflowId, versionNumber, configSnapshot);

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
