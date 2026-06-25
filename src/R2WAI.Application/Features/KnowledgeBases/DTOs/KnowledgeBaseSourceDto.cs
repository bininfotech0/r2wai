namespace R2WAI.Application.Features.KnowledgeBases.DTOs;

public class KnowledgeBaseSourceDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public Guid? ReferenceId { get; init; }
    public string? Url { get; init; }
    public string? Content { get; init; }
    public string? Status { get; init; }
    public DateTime CreatedAt { get; init; }
}
