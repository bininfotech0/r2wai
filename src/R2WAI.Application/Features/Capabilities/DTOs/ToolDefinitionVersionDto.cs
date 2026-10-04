namespace R2WAI.Application.Features.Capabilities.DTOs;

public class ToolDefinitionVersionDto
{
    public Guid Id { get; init; }
    public Guid ToolDefinitionId { get; init; }
    public int VersionNumber { get; init; }
    public string ConfigSnapshot { get; init; } = string.Empty;
    public bool IsPublished { get; init; }
    public string? Note { get; init; }
    public Guid? PublishedByUserId { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
