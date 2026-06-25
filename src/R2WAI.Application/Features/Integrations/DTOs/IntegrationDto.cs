namespace R2WAI.Application.Features.Integrations.DTOs;

public class IntegrationDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? EndpointUrl { get; init; }
    public string? Configuration { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}
