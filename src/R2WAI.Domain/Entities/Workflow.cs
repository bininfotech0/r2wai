using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class Workflow : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Type { get; private set; }
    public string? Trigger { get; private set; }
    public string? Steps { get; private set; }
    public bool IsActive { get; private set; }
    public int Version { get; private set; } = 1;
    public string VersionStatus { get; private set; } = "Draft";

    public Tenant Tenant { get; private set; } = null!;
    public User User { get; private set; } = null!;
    public ICollection<WorkflowInstance> Instances { get; private set; } = [];

    private Workflow() { }

    public Workflow(Guid id, Guid tenantId, Guid userId, string name,
                    string? description = null, string? type = null,
                    string? steps = null)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Name = name;
        Description = description;
        Type = type;
        Steps = steps;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, string? type,
                               string? trigger, string? steps)
    {
        Name = name;
        Description = description;
        Type = type;
        Trigger = trigger;
        Steps = steps;
        MarkAsModified();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsModified();
    }

    public void Pause()
    {
        IsActive = false;
        MarkAsModified();
    }

    public void Publish()
    {
        VersionStatus = "Published";
        IsActive = true;
        MarkAsModified();
    }

    public void Unpublish()
    {
        VersionStatus = "Draft";
        IsActive = false;
        MarkAsModified();
    }

    public void Archive()
    {
        VersionStatus = "Archived";
        IsActive = false;
        MarkAsModified();
    }

    public void Restore()
    {
        VersionStatus = "Draft";
        IsActive = false;
        MarkAsModified();
    }

    public void NewVersion()
    {
        Version++;
        VersionStatus = "Draft";
        MarkAsModified();
    }
}
