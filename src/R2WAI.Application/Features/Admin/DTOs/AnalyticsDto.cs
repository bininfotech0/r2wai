namespace R2WAI.Application.Features.Admin.DTOs;

public class AnalyticsDto
{
    public int TotalUsers { get; init; }
    public int ActiveConversations { get; init; }
    public int TotalDocuments { get; init; }
    public int TotalChatbots { get; init; }
    public int TotalKnowledgeBases { get; init; }
    public int TotalWorkflows { get; init; }
    public int AiRequestsToday { get; init; }
    public Dictionary<string, int> RequestsByModule { get; init; } = [];
}
