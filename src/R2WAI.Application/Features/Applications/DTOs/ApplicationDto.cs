namespace R2WAI.Application.Features.Applications.DTOs;

public class ApplicationDto
{
    public Guid Id { get; init; }
    public Guid? DepartmentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? BaseUrl { get; init; }
    public string Environment { get; init; } = "Development";
    public string Status { get; init; } = "Draft";
    public Guid? ManagerUserId { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}
