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

    // Real, rolling 24h operational signals — see IRequestMetricsStore.
    public int TotalRequests { get; init; }
    public double SuccessRate { get; init; }
    public double AverageLatencyMs { get; init; }
    public int ActiveUsers { get; init; }
    public int ApiErrors { get; init; }
    public int AiErrors { get; init; }
    public int WorkflowErrors { get; init; }
}
