using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// The container a tenant's agents, knowledge, automations, published chatbots and connected
/// systems belong to. A narrowing of the tenant boundary, never a widening: every workspace-scoped
/// row keeps its own TenantId. Each tenant has exactly one default workspace, which receives every
/// resource created without an explicit workspace (see ApplicationDbContext.AssignDefaultWorkspaces)
/// and can be neither deleted nor demoted.
/// </summary>
public sealed class Workspace : BaseEntity<Guid>
{
    public const string DefaultName = "Default workspace";

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private Workspace() { }

    public Workspace(Guid id, Guid tenantId, string name, string? description, Guid? createdByUserId, bool isDefault = false)
    {
        Id = id;
        TenantId = tenantId;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CreatedByUserId = createdByUserId;
        IsDefault = isDefault;
        CreatedAt = DateTime.UtcNow;
    }

    public static Workspace CreateDefault(Guid tenantId) =>
        new(Guid.NewGuid(), tenantId, DefaultName, "Created automatically. Holds everything not assigned to another workspace.", null, isDefault: true);

    public void UpdateDetails(string name, string? description)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        MarkAsModified();
    }
}
