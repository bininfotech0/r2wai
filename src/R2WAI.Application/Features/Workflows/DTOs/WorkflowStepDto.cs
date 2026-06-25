namespace R2WAI.Application.Features.Workflows.DTOs;

public class WorkflowStepDto
{
    public int Order { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AssignedRole { get; init; }
    public string? Action { get; init; }
    public WorkflowStepStatus Status { get; init; }
    public string? Comments { get; init; }
    public DateTime? CompletedAt { get; init; }
}
