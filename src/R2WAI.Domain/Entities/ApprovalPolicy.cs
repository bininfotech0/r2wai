using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class ApprovalPolicy : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? WorkflowType { get; private set; }
    public string? ApproverRoles { get; private set; }
    public int MinApprovers { get; private set; }
    public int? EscalationMinutes { get; private set; }
    public string? EscalationRoles { get; private set; }
    public bool IsActive { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private ApprovalPolicy() { }

    public ApprovalPolicy(Guid id, Guid tenantId, string name, string? description = null,
        string? workflowType = null, string? approverRoles = null,
        int minApprovers = 1, int? escalationMinutes = null,
        string? escalationRoles = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Description = description;
        WorkflowType = workflowType;
        ApproverRoles = approverRoles;
        MinApprovers = minApprovers;
        EscalationMinutes = escalationMinutes;
        EscalationRoles = escalationRoles;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string? description, string? workflowType,
        string? approverRoles, int minApprovers, int? escalationMinutes,
        string? escalationRoles)
    {
        Name = name;
        Description = description;
        WorkflowType = workflowType;
        ApproverRoles = approverRoles;
        MinApprovers = minApprovers;
        EscalationMinutes = escalationMinutes;
        EscalationRoles = escalationRoles;
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
