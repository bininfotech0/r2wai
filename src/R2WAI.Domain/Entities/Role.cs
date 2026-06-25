using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class Role : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Permissions { get; private set; }
    public bool IsSystem { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ICollection<UserRole> UserRoles { get; private set; } = [];

    private Role() { }

    public Role(Guid id, Guid tenantId, string name, string? description = null, bool isSystem = false)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Description = description;
        IsSystem = isSystem;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
        MarkAsModified();
    }

    public void SetPermissions(string permissions)
    {
        Permissions = permissions;
        MarkAsModified();
    }
}
