namespace R2WAI.Application.Features.Applications.DTOs;

public class ApplicationConfigurationDto
{
    public Guid Id { get; init; }
    public Guid ApplicationId { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 3;
    public double RagThreshold { get; init; } = 0.7;
    public string? ModelId { get; init; }
    public string? SystemPromptTemplate { get; init; }
    public DateTime? ModifiedAt { get; init; }
}
