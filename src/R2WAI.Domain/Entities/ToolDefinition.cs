using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class ToolDefinition : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public Guid? ApplicationApiId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public ToolType ToolType { get; private set; }
    public string? EndpointUrl { get; private set; }
    public string? HttpMethod { get; private set; }
    public string? EndpointPath { get; private set; }
    public string? Configuration { get; private set; }
    public bool IsActive { get; private set; }

    // Governance — every capability carries these regardless of how it was created (§19).
    public string RiskLevel { get; private set; } = "Low";
    public string? RequiredRole { get; private set; }
    public bool ConfirmationRequired { get; private set; }
    public bool ApprovalRequired { get; private set; }
    public bool AuditRequired { get; private set; } = true;

    public Tenant Tenant { get; private set; } = null!;
    public ConnectedApplication? Application { get; private set; }
    public ApplicationApi? ApplicationApi { get; private set; }

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

    public void AssignApplication(Guid? applicationId)
    {
        ApplicationId = applicationId;
        MarkAsModified();
    }

    public void LinkApi(Guid? applicationApiId, string? httpMethod, string? endpointPath)
    {
        ApplicationApiId = applicationApiId;
        HttpMethod = httpMethod;
        EndpointPath = endpointPath;
        MarkAsModified();
    }

    public void ConfigureGovernance(string riskLevel, string? requiredRole,
        bool confirmationRequired, bool approvalRequired, bool auditRequired)
    {
        RiskLevel = riskLevel;
        RequiredRole = requiredRole;
        ConfirmationRequired = confirmationRequired;
        ApprovalRequired = approvalRequired;
        AuditRequired = auditRequired;
        MarkAsModified();
    }
}
