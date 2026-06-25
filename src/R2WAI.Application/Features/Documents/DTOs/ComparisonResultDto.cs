namespace R2WAI.Application.Features.Documents.DTOs;

public class ComparisonResultDto
{
    public Guid SourceDocumentId { get; init; }
    public Guid TargetDocumentId { get; init; }
    public string Comparison { get; init; } = string.Empty;
    public string? Differences { get; init; }
    public string? Similarities { get; init; }
    public double? SimilarityScore { get; init; }
}
