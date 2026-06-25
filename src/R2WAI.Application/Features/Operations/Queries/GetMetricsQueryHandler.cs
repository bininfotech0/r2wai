namespace R2WAI.Application.Features.Operations.Queries;

public class GetMetricsQueryHandler(
    IRepository<WorkflowInstance> instanceRepo,
    IRepository<Domain.Entities.Workflow> workflowRepo,
    IRepository<Document> documentRepo,
    IRepository<KnowledgeBase> kbRepo,
    IRepository<AssistantDefinition> assistantRepo,
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

        var totalWorkflowsTask = workflowRepo.CountAsync(
            w => w.TenantId == tenantId && !w.IsDeleted, cancellationToken);
        var activeWorkflowsTask = instanceRepo.CountAsync(
            i => i.TenantId == tenantId && i.Status == WorkflowInstanceStatus.Running, cancellationToken);
        var completedTodayTask = instanceRepo.CountAsync(
            i => i.TenantId == tenantId
                 && i.Status == WorkflowInstanceStatus.Completed
                 && i.CompletedAt != null
                 && i.CompletedAt >= startOfDayUtc, cancellationToken);
        var totalDocumentsTask = documentRepo.CountAsync(
            d => d.TenantId == tenantId && !d.IsDeleted, cancellationToken);
        var totalKnowledgeBasesTask = kbRepo.CountAsync(
            kb => kb.TenantId == tenantId && !kb.IsDeleted, cancellationToken);
        var totalAssistantsTask = assistantRepo.CountAsync(
            a => a.TenantId == tenantId && !a.IsDeleted, cancellationToken);

        await Task.WhenAll(totalWorkflowsTask, activeWorkflowsTask, completedTodayTask,
            totalDocumentsTask, totalKnowledgeBasesTask, totalAssistantsTask);

        var totalWorkflows = await totalWorkflowsTask;
        var activeWorkflows = await activeWorkflowsTask;
        var completedToday = await completedTodayTask;
        var totalDocuments = await totalDocumentsTask;
        var totalKnowledgeBases = await totalKnowledgeBasesTask;
        var totalAssistants = await totalAssistantsTask;

        var result = new MetricsDto
        {
            TotalWorkflows = totalWorkflows,
            ActiveWorkflows = activeWorkflows,
            TotalDocuments = totalDocuments,
            TotalKnowledgeBases = totalKnowledgeBases,
            TotalAssistants = totalAssistants,
            CompletedToday = completedToday,
            Timestamp = DateTime.UtcNow,
        };

        await cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(30), cancellationToken);
        return result;
    }
}
