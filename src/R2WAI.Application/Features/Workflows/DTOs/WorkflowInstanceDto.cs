namespace R2WAI.Application.Features.Workflows.DTOs;

public class WorkflowInstanceDto
{
    public Guid Id { get; init; }
    public Guid WorkflowId { get; init; }
    public string? WorkflowName { get; set; }
    public WorkflowInstanceStatus Status { get; init; }
    public int CurrentStep { get; init; }
    public string? Data { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<WorkflowStepDto> Steps { get; init; } = [];
}
