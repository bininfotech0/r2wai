using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class WorkflowInstance : BaseEntity<Guid>
{
    public Guid WorkflowId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InitiatedBy { get; private set; }

    // Which Workflow.Version this instance actually started running against — Steps gets overwritten
    // in place as the workflow is edited/re-versioned, so this is what lets a running/completed
    // instance's real executed content be resolved later via WorkflowVersion, instead of silently
    // reading whatever the workflow's content happens to be *now*.
    public int WorkflowVersionNumber { get; private set; }
    public WorkflowInstanceStatus Status { get; private set; } = WorkflowInstanceStatus.Running;
    public int CurrentStep { get; private set; }
    public string? Data { get; private set; }
    public string? ElsaInstanceId { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // R2WAI's own delayed-continuation state for a Delay step (see WorkflowBridge.ResumeWorkflowAsync's
    // doc comment) — Elsa's native Delay+bookmark resume doesn't work reliably here, so a Delay step
    // never actually runs as a real Elsa activity; instead the instance sits here until
    // WorkflowDelayResumeBackgroundService picks it up and continues execution from PendingResumeStepIndex.
    public DateTime? PendingResumeAt { get; private set; }
    public int? PendingResumeStepIndex { get; private set; }

    // Implementation plan Phase 5: WorkflowDelayResumeBackgroundService's atomic claim used to null
    // out PendingResumeAt itself as the claim signal — correct against a *concurrent* replica, but
    // if the claiming replica then crashed before ContinueDelayedWorkflowAsync finished, the
    // instance was left with PendingResumeAt cleared and PendingResumeStepIndex still set: looks
    // like "not pending a resume" from every query that matters, silently abandoning it forever
    // with no error and no distinguishable stuck status. The lease preserves PendingResumeAt as the
    // durable "this needs resuming" signal; only this field marks a claim in flight, and only its
    // expiry decides whether that claim is stale.
    public DateTime? PendingResumeLeaseExpiresAt { get; private set; }

    public Workflow Workflow { get; private set; } = null!;
    public Tenant Tenant { get; private set; } = null!;
    public User Initiator { get; private set; } = null!;

    private WorkflowInstance() { }

    public WorkflowInstance(Guid id, Guid workflowId, Guid tenantId, Guid initiatedBy,
                             string? data = null, int workflowVersionNumber = 1)
    {
        Id = id;
        WorkflowId = workflowId;
        TenantId = tenantId;
        InitiatedBy = initiatedBy;
        Data = data;
        WorkflowVersionNumber = workflowVersionNumber;
        StartedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    public void AdvanceStep()
    {
        CurrentStep++;
        MarkAsModified();
    }

    public void Complete()
    {
        Status = WorkflowInstanceStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Fail()
    {
        Status = WorkflowInstanceStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Cancel()
    {
        Status = WorkflowInstanceStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void SetElsaInstanceId(string elsaInstanceId)
    {
        ElsaInstanceId = elsaInstanceId;
        MarkAsModified();
    }

    public void UpdateData(string data)
    {
        Data = data;
        MarkAsModified();
    }

    public void ScheduleDelayedResume(DateTime resumeAt, int stepIndex)
    {
        PendingResumeAt = resumeAt;
        PendingResumeStepIndex = stepIndex;
        MarkAsModified();
    }

    public void ClearPendingResume()
    {
        PendingResumeAt = null;
        PendingResumeStepIndex = null;
        PendingResumeLeaseExpiresAt = null;
        MarkAsModified();
    }

    // Called before re-running a Failed instance (WorkflowBridge.RetryFailedStepAsync). Without this,
    // the instance stays Failed from the first attempt, and WorkflowInstanceCompletionNotificationHandler's
    // idempotency guard ("only transition out of Running") silently ignores the retry's own completion
    // notification — confirmed live: a retried step genuinely re-executed and re-faulted, but neither
    // the instance nor the step's WorkflowStepExecution row ever recorded the second outcome.
    public void Retry()
    {
        Status = WorkflowInstanceStatus.Running;
        CompletedAt = null;
        MarkAsModified();
    }
}
