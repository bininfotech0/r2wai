namespace R2WAI.Application.Features.Admin.DTOs;

public class RoleDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Permissions { get; init; }
    public bool IsSystem { get; init; }
    public DateTime CreatedAt { get; init; }
}
