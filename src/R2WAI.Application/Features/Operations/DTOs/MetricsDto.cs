namespace R2WAI.Application.Features.Operations.DTOs;

public class MetricsDto
{
    public int TotalWorkflows { get; init; }
    public int ActiveWorkflows { get; init; }
    public int TotalDocuments { get; init; }
    public int TotalKnowledgeBases { get; init; }
    public int TotalAssistants { get; init; }
    public int TotalConversations { get; init; }
    public int CompletedToday { get; init; }
    public DateTime Timestamp { get; init; }

    // Login-based active users (persisted, comparable week-over-week) — distinct from
    // the rolling 24h HTTP-activity ActiveUsers below, which resets on app restart.
    public int ActiveUsersByLogin { get; init; }

    // Completed / (Completed + Failed) workflow runs over the last 30 days. Null when
    // there are no completed-or-failed runs yet in that window (nothing to divide).
    public double? WorkflowSuccessRatePercent { get; init; }

    // Week-over-week % change (this 7d window vs the preceding 7d window). Null when the
    // prior window has zero baseline — an infinite/undefined percent change isn't shown.
    public double? ApplicationsTrendPercent { get; init; }
    public double? AssistantsTrendPercent { get; init; }
    public double? ConversationsTrendPercent { get; init; }
    public double? ActiveUsersTrendPercent { get; init; }

    // Real, rolling 24h operational signals — see IRequestMetricsStore.
    public int TotalRequests { get; init; }
    public double SuccessRate { get; init; }
    public double AverageLatencyMs { get; init; }
    public int ActiveUsers { get; init; }
    public int ApiErrors { get; init; }
    public int AiErrors { get; init; }
    public int WorkflowErrors { get; init; }
    public int AiRequests { get; init; }
}
