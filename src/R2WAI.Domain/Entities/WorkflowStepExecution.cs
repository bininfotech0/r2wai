using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class WorkflowStepExecution : BaseEntity<Guid>
{
    public Guid WorkflowInstanceId { get; private set; }
    public int StepIndex { get; private set; }
    public string StepName { get; private set; }
    public string? StepType { get; private set; }
    public WorkflowStepStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Output { get; private set; }
    public string? Error { get; private set; }
    public string? Variables { get; private set; }

    public WorkflowInstance WorkflowInstance { get; private set; } = null!;

    private WorkflowStepExecution() { StepName = string.Empty; }

    public WorkflowStepExecution(Guid id, Guid workflowInstanceId, int stepIndex,
        string stepName, string? stepType = null)
    {
        Id = id;
        WorkflowInstanceId = workflowInstanceId;
        StepIndex = stepIndex;
        StepName = stepName;
        StepType = stepType;
        Status = WorkflowStepStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        Status = WorkflowStepStatus.Running;
        StartedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Complete(string? output = null, string? variables = null)
    {
        Status = WorkflowStepStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Output = output;
        Variables = variables;
        MarkAsModified();
    }

    public void Fail(string error)
    {
        Status = WorkflowStepStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        Error = error;
        MarkAsModified();
    }

    public void Skip()
    {
        Status = WorkflowStepStatus.Skipped;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }
}
