namespace R2WAI.Application.Features.Documents.DTOs;

public class DocumentDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DocumentType FileType { get; init; }
    public long FileSize { get; init; }
    public DocumentStatus Status { get; init; }
    public string? ProcessingError { get; init; }
    public int? PageCount { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
    public DateTime CreatedAt { get; init; }
}
