using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class Department : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Description { get; private set; }
    public Guid? HeadUserId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public Tenant Tenant { get; private set; } = null!;
    public ICollection<ConnectedApplication> Applications { get; private set; } = [];

    private Department() { }

    public Department(Guid id, Guid tenantId, string name, string code, string? description = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Code = code;
        Description = description;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
        MarkAsModified();
    }

    public void SetHead(Guid? headUserId)
    {
        HeadUserId = headUserId;
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
}
