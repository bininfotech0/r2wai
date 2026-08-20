namespace R2WAI.Application.Features.Members.DTOs;

public class MemberEventDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int PointsValue { get; init; }
    public DateTime EventDate { get; init; }
    public bool IsActive { get; init; }
    public int AttendanceCount { get; init; }
    public DateTime CreatedAt { get; init; }
}
