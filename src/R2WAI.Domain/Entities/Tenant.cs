using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class Tenant : BaseEntity<Guid>
{
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string? Domain { get; private set; }
    public TenantStatus Status { get; private set; } = TenantStatus.Active;
    public string? Features { get; private set; }
    public string? Settings { get; private set; }

    public ICollection<User> Users { get; private set; } = [];
    public ICollection<Role> Roles { get; private set; } = [];

    private Tenant() { }

    public Tenant(Guid id, string name, string slug, string? domain = null)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Domain = domain;
        Status = TenantStatus.Active;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string slug, string? domain)
    {
        Name = name;
        Slug = slug;
        Domain = domain;
        MarkAsModified();
    }

    public void UpdateStatus(TenantStatus status)
    {
        Status = status;
        MarkAsModified();
    }

    public void UpdateFeatures(string features)
    {
        Features = features;
        MarkAsModified();
    }

    public void UpdateSettings(string settings)
    {
        Settings = settings;
        MarkAsModified();
    }
}
