using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class WebhookEndpoint : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string TriggerType { get; private set; } = "Workflow";
    public Guid? WorkflowId { get; private set; }
    public string? Secret { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastCalledAt { get; private set; }
    public int TotalCalls { get; private set; }

    public Workflow? Workflow { get; private set; }

    private WebhookEndpoint() { }

    public WebhookEndpoint(Guid id, Guid tenantId, string name, string slug,
                            string triggerType, Guid? workflowId = null, string? secret = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Slug = slug;
        TriggerType = triggerType;
        WorkflowId = workflowId;
        Secret = secret;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string triggerType, Guid? workflowId, string? secret)
    {
        Name = name;
        TriggerType = triggerType;
        WorkflowId = workflowId;
        Secret = secret;
        MarkAsModified();
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        MarkAsModified();
    }

    public void RecordCall()
    {
        LastCalledAt = DateTime.UtcNow;
        TotalCalls++;
        MarkAsModified();
    }
}
