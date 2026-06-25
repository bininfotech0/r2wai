namespace R2WAI.Application.Features.Admin.DTOs;

public class ModelConfigDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string? Endpoint { get; init; }
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }
    public double? TopP { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
    public bool HasApiKey { get; init; }
    public DateTime CreatedAt { get; init; }
}
