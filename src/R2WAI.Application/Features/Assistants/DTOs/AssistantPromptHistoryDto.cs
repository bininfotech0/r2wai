namespace R2WAI.Application.Features.Assistants.DTOs;

public class AssistantPromptHistoryDto
{
    public Guid Id { get; init; }
    public Guid AssistantDefinitionId { get; init; }
    public string Content { get; init; } = string.Empty;
    public int Version { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}
