namespace R2WAI.Application.Features.KnowledgeBases.DTOs;

public class SearchResultDto
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? SourceName { get; init; }
    public double Score { get; init; }
    public string? Metadata { get; init; }
}
