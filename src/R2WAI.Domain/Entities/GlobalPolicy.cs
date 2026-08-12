using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class GlobalPolicy : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Type { get; private set; }
    public string Name { get; private set; }
    public string? Content { get; private set; }
    public bool IsActive { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private GlobalPolicy() { }

    public GlobalPolicy(Guid id, Guid tenantId, string type, string name, string? content, bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        Type = type;
        Name = name;
        Content = content;
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string? content, bool isActive)
    {
        Name = name;
        Content = content;
        IsActive = isActive;
        MarkAsModified();
    }
}
