namespace R2WAI.Application.Features.Workflows.DTOs;

public class WorkflowDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Type { get; init; }
    public string? Trigger { get; init; }
    public string? Steps { get; init; }
    public bool IsActive { get; init; }
    public int Version { get; init; }
    public string VersionStatus { get; init; } = "Draft";
    public bool IsArchived => VersionStatus == "Archived";
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}
