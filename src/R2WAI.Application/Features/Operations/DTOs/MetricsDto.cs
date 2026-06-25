namespace R2WAI.Application.Features.Operations.DTOs;

public class MetricsDto
{
    public int TotalWorkflows { get; init; }
    public int ActiveWorkflows { get; init; }
    public int TotalDocuments { get; init; }
    public int TotalKnowledgeBases { get; init; }
    public int TotalAssistants { get; init; }
    public int CompletedToday { get; init; }
    public DateTime Timestamp { get; init; }
}
