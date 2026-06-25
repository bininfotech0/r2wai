using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class ToolDefinition : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public ToolType ToolType { get; private set; }
    public string? EndpointUrl { get; private set; }
    public string? Configuration { get; private set; }
    public bool IsActive { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private ToolDefinition() { }

    public ToolDefinition(Guid id, Guid tenantId, string name, ToolType toolType,
        string? description = null, string? endpointUrl = null,
        string? configuration = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        ToolType = toolType;
        Description = description;
        EndpointUrl = endpointUrl;
        Configuration = configuration;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string? description, ToolType toolType,
        string? endpointUrl, string? configuration)
    {
        Name = name;
        Description = description;
        ToolType = toolType;
        EndpointUrl = endpointUrl;
        Configuration = configuration;
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
