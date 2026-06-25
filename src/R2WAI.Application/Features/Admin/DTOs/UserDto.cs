namespace R2WAI.Application.Features.Admin.DTOs;

public class UserDto
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string? AvatarUrl { get; init; }
    public string? Status { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<string> Roles { get; init; } = [];
}
