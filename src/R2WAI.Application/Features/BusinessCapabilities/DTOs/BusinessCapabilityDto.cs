namespace R2WAI.Application.Features.BusinessCapabilities.DTOs;

public class BusinessCapabilityDto
{
    public Guid Id { get; init; }
    public Guid AssistantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string Status { get; init; } = "Draft";
    public IReadOnlyList<Guid> ToolIds { get; init; } = [];
    public IReadOnlyList<Guid> KnowledgeBaseIds { get; init; } = [];
    public IReadOnlyList<Guid> WorkflowIds { get; init; } = [];
    public IReadOnlyList<Guid> ApplicationApiIds { get; init; } = [];
    public int ToolCount => ToolIds.Count;
    public int KnowledgeBaseCount => KnowledgeBaseIds.Count;
    public int WorkflowCount => WorkflowIds.Count;
    public int ApiCount => ApplicationApiIds.Count;
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}
