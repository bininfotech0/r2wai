namespace R2WAI.Application.Features.KnowledgeBases.DTOs;

public class KnowledgeBaseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public KnowledgeBaseStatus Status { get; init; }
    public string? EmbeddingModel { get; init; }
    public int? ChunkSize { get; init; }
    public int? ChunkOverlap { get; init; }
    public int DocumentCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<KnowledgeBaseSourceDto> Sources { get; init; } = [];
}
