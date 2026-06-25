namespace R2WAI.Application.Features.Documents.DTOs;

public class DocumentSummaryDto
{
    public Guid DocumentId { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string? KeyPoints { get; init; }
    public int? WordCount { get; init; }
}
