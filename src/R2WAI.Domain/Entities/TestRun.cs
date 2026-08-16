using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class TestRun : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid? AssistantId { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public Guid TriggeredByUserId { get; private set; }
    public TestRunStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int PassedCount { get; private set; }
    public int FailedCount { get; private set; }
    public int WarningCount { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public AssistantDefinition? Assistant { get; private set; }
    public ConnectedApplication? Application { get; private set; }
    public ICollection<TestCaseResult> Results { get; private set; } = new List<TestCaseResult>();

    private TestRun() { }

    public TestRun(Guid id, Guid tenantId, Guid triggeredByUserId, Guid? assistantId = null, Guid? applicationId = null)
    {
        Id = id;
        TenantId = tenantId;
        TriggeredByUserId = triggeredByUserId;
        AssistantId = assistantId;
        ApplicationId = applicationId;
        Status = TestRunStatus.Running;
        StartedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    public void Complete(int passedCount, int failedCount, int warningCount)
    {
        PassedCount = passedCount;
        FailedCount = failedCount;
        WarningCount = warningCount;
        Status = TestRunStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }
}
