namespace R2WAI.Application.Features.Documents.DTOs;

public class ExtractionResultDto
{
    public Guid DocumentId { get; init; }
    public string ExtractedData { get; init; } = string.Empty;
    public string Schema { get; init; } = string.Empty;
    public int FieldCount { get; init; }
}
