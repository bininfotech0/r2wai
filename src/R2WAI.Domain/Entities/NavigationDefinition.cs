using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class NavigationDefinition : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Label { get; private set; }
    public string? Icon { get; private set; }
    public string Path { get; private set; }
    public string? RequiredRole { get; private set; }
    public bool IsExternal { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public int Order { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ConnectedApplication Application { get; private set; } = null!;

    private NavigationDefinition() { }

    public NavigationDefinition(Guid id, Guid tenantId, Guid applicationId, string label, string path, int order)
    {
        Id = id;
        TenantId = tenantId;
        ApplicationId = applicationId;
        Label = label;
        Path = path;
        Order = order;
        IsEnabled = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string label, string? icon, string path, string? requiredRole, bool isExternal)
    {
        Label = label;
        Icon = icon;
        Path = path;
        RequiredRole = requiredRole;
        IsExternal = isExternal;
        MarkAsModified();
    }

    public void SetOrder(int order)
    {
        Order = order;
        MarkAsModified();
    }

    public void Enable()
    {
        IsEnabled = true;
        MarkAsModified();
    }

    public void Disable()
    {
        IsEnabled = false;
        MarkAsModified();
    }
}
