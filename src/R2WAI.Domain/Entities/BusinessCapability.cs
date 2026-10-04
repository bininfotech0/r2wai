using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A business-oriented bundle of existing Tools/APIs/Knowledge/Workflows under one assistant
/// (e.g. "Invoice Management" on a Supplier Assistant) — the redesign brief's user-facing
/// "Capability" concept. Deliberately a distinct entity from ToolDefinition (which the
/// Capabilities feature/CapabilityDto already models as a single governed tool operation, and
/// which the UI has always labeled "Tool", never "Capability") so the two meanings of the word
/// don't collide.
/// </summary>
public sealed class BusinessCapability : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid AssistantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public BusinessCapabilityStatus Status { get; private set; } = BusinessCapabilityStatus.Draft;

    // JSON-encoded Guid[] — same convention as AssistantDefinition.Tools / Chatbot.AllowedOrigins:
    // null means "never configured", "[]" means "explicitly linked to nothing".
    public string? ToolIds { get; private set; }
    public string? KnowledgeBaseIds { get; private set; }
    public string? WorkflowIds { get; private set; }
    public string? ApplicationApiIds { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public AssistantDefinition Assistant { get; private set; } = null!;

    private BusinessCapability() { }

    public BusinessCapability(Guid id, Guid tenantId, Guid assistantId, string name, string? description = null)
    {
        Id = id;
        TenantId = tenantId;
        AssistantId = assistantId;
        Name = name;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, string? icon)
    {
        Name = name;
        Description = description;
        Icon = icon;
        MarkAsModified();
    }

    public void SetStatus(BusinessCapabilityStatus status)
    {
        Status = status;
        MarkAsModified();
    }

    public void LinkResources(IReadOnlyCollection<Guid>? toolIds, IReadOnlyCollection<Guid>? knowledgeBaseIds,
        IReadOnlyCollection<Guid>? workflowIds, IReadOnlyCollection<Guid>? applicationApiIds)
    {
        ToolIds = toolIds is null ? null : System.Text.Json.JsonSerializer.Serialize(toolIds);
        KnowledgeBaseIds = knowledgeBaseIds is null ? null : System.Text.Json.JsonSerializer.Serialize(knowledgeBaseIds);
        WorkflowIds = workflowIds is null ? null : System.Text.Json.JsonSerializer.Serialize(workflowIds);
        ApplicationApiIds = applicationApiIds is null ? null : System.Text.Json.JsonSerializer.Serialize(applicationApiIds);
        MarkAsModified();
    }

    public IReadOnlyList<Guid> GetToolIds() => ParseIds(ToolIds);
    public IReadOnlyList<Guid> GetKnowledgeBaseIds() => ParseIds(KnowledgeBaseIds);
    public IReadOnlyList<Guid> GetWorkflowIds() => ParseIds(WorkflowIds);
    public IReadOnlyList<Guid> GetApplicationApiIds() => ParseIds(ApplicationApiIds);

    private static IReadOnlyList<Guid> ParseIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}
