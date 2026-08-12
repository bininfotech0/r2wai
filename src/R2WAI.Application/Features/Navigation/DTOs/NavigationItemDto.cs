namespace R2WAI.Application.Features.Navigation.DTOs;

public class NavigationItemDto
{
    public Guid Id { get; init; }
    public Guid ApplicationId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? RequiredRole { get; init; }
    public bool IsExternal { get; init; }
    public bool IsEnabled { get; init; }
    public int Order { get; init; }
    public DateTime CreatedAt { get; init; }
}
