namespace R2WAI.Application.Features.Operations.Queries;

public class GetMetricsQueryHandler(
    IRepository<WorkflowInstance> instanceRepo,
    IRepository<Domain.Entities.Workflow> workflowRepo,
    IRepository<Document> documentRepo,
    IRepository<KnowledgeBase> kbRepo,
    IRepository<AssistantDefinition> assistantRepo,
    IRepository<ConnectedApplication> applicationRepo,
    IRepository<Conversation> conversationRepo,
    IRepository<User> userRepo,
    IRequestMetricsStore requestMetrics,
    ICurrentUserService currentUser,
    Common.Interfaces.ICacheService cache) : IRequestHandler<GetMetricsQuery, MetricsDto>
{
    public async Task<MetricsDto> Handle(GetMetricsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var cacheKey = $"metrics:{tenantId}";

        var cached = await cache.GetAsync<MetricsDto>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var startOfDayUtc = DateTime.UtcNow.Date;
        var last24Hours = DateTime.UtcNow.AddHours(-24);
        var last30Days = DateTime.UtcNow.AddDays(-30);
        var now = DateTime.UtcNow;
        var oneWeekAgo = now.AddDays(-7);
        var twoWeeksAgo = now.AddDays(-14);

        // Sequential queries — EF Core DbContext is not thread-safe; Task.WhenAll would throw
        var totalWorkflows    = await workflowRepo.CountAsync(
            w => w.TenantId == tenantId && !w.IsDeleted, cancellationToken);
        var activeWorkflows   = await instanceRepo.CountAsync(
            i => i.TenantId == tenantId && i.Status == WorkflowInstanceStatus.Running, cancellationToken);
        var completedToday    = await instanceRepo.CountAsync(
            i => i.TenantId == tenantId
                 && i.Status == WorkflowInstanceStatus.Completed
                 && i.CompletedAt != null
                 && i.CompletedAt >= startOfDayUtc, cancellationToken);
        var totalDocuments    = await documentRepo.CountAsync(
            d => d.TenantId == tenantId && !d.IsDeleted, cancellationToken);
        var totalKnowledgeBases = await kbRepo.CountAsync(
            kb => kb.TenantId == tenantId && !kb.IsDeleted, cancellationToken);
        var totalAssistants   = await assistantRepo.CountAsync(
            a => a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        var totalConversations = await conversationRepo.CountAsync(
            c => c.TenantId == tenantId, cancellationToken);
        var workflowErrors = await instanceRepo.CountAsync(
            i => i.TenantId == tenantId
                 && i.Status == WorkflowInstanceStatus.Failed
                 && i.CompletedAt != null
                 && i.CompletedAt >= last24Hours, cancellationToken);

        var completedLast30d = await instanceRepo.CountAsync(
            i => i.TenantId == tenantId && i.Status == WorkflowInstanceStatus.Completed && i.CreatedAt >= last30Days, cancellationToken);
        var failedLast30d = await instanceRepo.CountAsync(
            i => i.TenantId == tenantId && i.Status == WorkflowInstanceStatus.Failed && i.CreatedAt >= last30Days, cancellationToken);
        var workflowSuccessRate = (completedLast30d + failedLast30d) > 0
            ? Math.Round(completedLast30d / (double)(completedLast30d + failedLast30d) * 100, 1)
            : (double?)null;

        var activeUsersByLogin = await userRepo.CountAsync(
            u => u.TenantId == tenantId && !u.IsDeleted && u.LastLoginAt != null && u.LastLoginAt >= oneWeekAgo, cancellationToken);
        var activeUsersPriorWeek = await userRepo.CountAsync(
            u => u.TenantId == tenantId && !u.IsDeleted && u.LastLoginAt != null && u.LastLoginAt >= twoWeeksAgo && u.LastLoginAt < oneWeekAgo, cancellationToken);

        var appsThisWeek = await applicationRepo.CountAsync(
            a => a.TenantId == tenantId && !a.IsDeleted && a.CreatedAt >= oneWeekAgo, cancellationToken);
        var appsPriorWeek = await applicationRepo.CountAsync(
            a => a.TenantId == tenantId && !a.IsDeleted && a.CreatedAt >= twoWeeksAgo && a.CreatedAt < oneWeekAgo, cancellationToken);

        var assistantsThisWeek = await assistantRepo.CountAsync(
            a => a.TenantId == tenantId && !a.IsDeleted && a.CreatedAt >= oneWeekAgo, cancellationToken);
        var assistantsPriorWeek = await assistantRepo.CountAsync(
            a => a.TenantId == tenantId && !a.IsDeleted && a.CreatedAt >= twoWeeksAgo && a.CreatedAt < oneWeekAgo, cancellationToken);

        var conversationsThisWeek = await conversationRepo.CountAsync(
            c => c.TenantId == tenantId && c.CreatedAt >= oneWeekAgo, cancellationToken);
        var conversationsPriorWeek = await conversationRepo.CountAsync(
            c => c.TenantId == tenantId && c.CreatedAt >= twoWeeksAgo && c.CreatedAt < oneWeekAgo, cancellationToken);

        var requestSnapshot = requestMetrics.GetSnapshot(tenantId, TimeSpan.FromHours(24));

        var result = new MetricsDto
        {
            TotalWorkflows = totalWorkflows,
            ActiveWorkflows = activeWorkflows,
            TotalDocuments = totalDocuments,
            TotalKnowledgeBases = totalKnowledgeBases,
            TotalAssistants = totalAssistants,
            TotalConversations = totalConversations,
            CompletedToday = completedToday,
            Timestamp = DateTime.UtcNow,
            ActiveUsersByLogin = activeUsersByLogin,
            WorkflowSuccessRatePercent = workflowSuccessRate,
            ApplicationsTrendPercent = ComputeTrendPercent(appsThisWeek, appsPriorWeek),
            AssistantsTrendPercent = ComputeTrendPercent(assistantsThisWeek, assistantsPriorWeek),
            ConversationsTrendPercent = ComputeTrendPercent(conversationsThisWeek, conversationsPriorWeek),
            ActiveUsersTrendPercent = ComputeTrendPercent(activeUsersByLogin, activeUsersPriorWeek),
            TotalRequests = requestSnapshot.TotalRequests,
            SuccessRate = requestSnapshot.SuccessRate,
            AverageLatencyMs = requestSnapshot.AverageLatencyMs,
            ActiveUsers = requestSnapshot.ActiveUsers,
            ApiErrors = requestSnapshot.ApiErrors,
            AiErrors = requestSnapshot.AiErrors,
            WorkflowErrors = workflowErrors,
            AiRequests = requestSnapshot.AiRequests,
        };

        await cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(30), cancellationToken);
        return result;
    }

    // Null when the prior period has no baseline to compare against — an infinite or
    // fabricated-looking percentage isn't shown; the UI omits the trend badge instead.
    private static double? ComputeTrendPercent(int current, int previous)
    {
        if (previous == 0) return null;
        return Math.Round((current - previous) / (double)previous * 100, 1);
    }
}
