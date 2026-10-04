using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A tenant-editable override of one of the hardcoded starter templates
/// (see WorkflowTemplateDefaults, the permanent fallback used when no override exists).
/// One row per tenant+TemplateId — unlike PromptTemplate, edits replace in place rather than
/// versioning; nothing in this codebase reads a prior workflow-template edit's history today.
/// </summary>
public sealed class WorkflowTemplateOverride : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string TemplateId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Type { get; private set; }

    /// <summary>JSON-serialized List&lt;WorkflowTemplateStepDto&gt; — same "structured data in a
    /// text column" pattern already used for ToolDefinition.Configuration/ApprovalRequest.Data.</summary>
    public string StepsJson { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private WorkflowTemplateOverride() { }

    public WorkflowTemplateOverride(Guid id, Guid tenantId, string templateId, string name,
                                     string? description, string type, string stepsJson)
    {
        Id = id;
        TenantId = tenantId;
        TemplateId = templateId;
        Name = name;
        Description = description;
        Type = type;
        StepsJson = stepsJson;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string? description, string type, string stepsJson)
    {
        Name = name;
        Description = description;
        Type = type;
        StepsJson = stepsJson;
        MarkAsModified();
    }
}
