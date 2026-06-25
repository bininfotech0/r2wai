namespace R2WAI.Application.Features.Assistants.DTOs;

public class AssistantDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public AssistantType Type { get; init; }
    public string? SystemPrompt { get; init; }
    public Guid? ModelConfigurationId { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
    public string? Tools { get; init; }
    public string? Settings { get; init; }
    public bool IsActive { get; init; }
    public string PublishStatus { get; init; } = "Draft";
    public int PublishedVersion { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? Tags { get; init; }
    public string? AvatarUrl { get; init; }
    public int UsageCount { get; init; }
    public DateTime CreatedAt { get; init; }
}
