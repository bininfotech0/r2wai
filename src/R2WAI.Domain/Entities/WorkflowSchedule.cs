using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class WorkflowSchedule : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid WorkflowId { get; private set; }
    public string Name { get; private set; }
    public string CronExpression { get; private set; }
    public string? CronDescription { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? NextRunAt { get; private set; }
    public DateTime? LastRunAt { get; private set; }
    public string? LastRunStatus { get; private set; }

    public Workflow Workflow { get; private set; } = null!;

    private WorkflowSchedule() { }

    public WorkflowSchedule(Guid id, Guid tenantId, Guid workflowId, string name,
                             string cronExpression, string? cronDescription = null)
    {
        Id = id;
        TenantId = tenantId;
        WorkflowId = workflowId;
        Name = name;
        CronExpression = cronExpression;
        CronDescription = cronDescription;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string cronExpression, string? cronDescription)
    {
        Name = name;
        CronExpression = cronExpression;
        CronDescription = cronDescription;
        MarkAsModified();
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        MarkAsModified();
    }

    public void RecordRun(string status)
    {
        LastRunAt = DateTime.UtcNow;
        LastRunStatus = status;
        MarkAsModified();
    }

    public void SetNextRun(DateTime? nextRun)
    {
        NextRunAt = nextRun;
    }
}
