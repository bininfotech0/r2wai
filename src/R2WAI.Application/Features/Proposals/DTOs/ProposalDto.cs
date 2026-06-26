namespace R2WAI.Application.Features.Proposals.DTOs;

public class ProposalDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = "Draft";
    public string? ClientName { get; init; }
    public DateTime? DueDate { get; init; }
    public string? GeneratedContent { get; init; }
    public int? WordCount { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
    public Guid? AssistantId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}
