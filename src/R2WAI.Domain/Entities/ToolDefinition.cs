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

    // ToolType.Mcp shape — mirrors ApplicationApiId/EndpointUrl's "either one or the other" duality:
    // this tool is one specific tool on the referenced McpServerConnection, identified by its real
    // MCP tool name (not this row's own admin-facing Name, which can differ).
    public Guid? McpServerConnectionId { get; private set; }
    public string? McpToolName { get; private set; }

    // Real, persisted connection status — replaces a boolean IsActive-only badge with a genuine
    // Connected/Error signal that survives a page reload. Null means "never tested," a distinct,
    // honest state from either outcome (Phase 5 UI shows all three, doesn't collapse "never tested"
    // into "Error").
    public string? LastTestStatus { get; private set; }
    public DateTime? LastTestedAt { get; private set; }

    // Governance — every capability carries these regardless of how it was created (§19).
    public string RiskLevel { get; private set; } = "Low";
    public string? RequiredRole { get; private set; }
    public bool ConfirmationRequired { get; private set; }
    public bool ApprovalRequired { get; private set; }
    public bool AuditRequired { get; private set; } = true;

    public Tenant Tenant { get; private set; } = null!;
    public ConnectedApplication? Application { get; private set; }
    public ApplicationApi? ApplicationApi { get; private set; }
    public McpServerConnection? McpServerConnection { get; private set; }

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

    public void LinkMcpServer(Guid mcpServerConnectionId, string mcpToolName)
    {
        McpServerConnectionId = mcpServerConnectionId;
        McpToolName = mcpToolName;
        MarkAsModified();
    }

    /// <summary>
    /// Conservative governance defaults derived from an HTTP method, for capabilities created
    /// automatically from an API description. Every such capability used to be created with the
    /// entity's defaults — risk "Low", no approval, active — so a discovered DELETE or POST was
    /// immediately callable by any assistant without a human ever having classified it. An unknown or
    /// missing method is treated as Medium.
    /// </summary>
    public static (string RiskLevel, bool ConfirmationRequired, bool ApprovalRequired) DefaultGovernanceForHttpMethod(string? httpMethod) =>
        (httpMethod ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "GET" or "HEAD" or "OPTIONS" => ("Low", false, false),
            "DELETE" => ("High", true, true),
            _ => ("Medium", true, false),
        };

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

    public void RecordTestResult(bool success)
    {
        LastTestStatus = success ? "Connected" : "Error";
        LastTestedAt = DateTime.UtcNow;
        MarkAsModified();
    }
}
