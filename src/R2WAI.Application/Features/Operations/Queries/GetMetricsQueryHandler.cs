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
