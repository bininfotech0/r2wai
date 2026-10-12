namespace R2WAI.Application.Features.Chatbots.Queries;

public record GetChatbotsQuery : IRequest<PagedResult<ChatbotDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? AssistantId { get; init; }
}

public class GetChatbotsQueryHandler(
    IRepository<Chatbot> chatbotRepo,
    ICurrentUserService currentUser,
    IWorkspaceContext workspace,
    ICacheService cache,
    IMapper mapper) : IRequestHandler<GetChatbotsQuery, PagedResult<ChatbotDto>>
{
    public async Task<PagedResult<ChatbotDto>> Handle(GetChatbotsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var workspaceId = workspace.WorkspaceId;
        var hasAssistantFilter = query.AssistantId.HasValue;
        // Workspace-filtered lists are not cached: the chatbot commands invalidate per tenant only.
        var cacheable = !hasAssistantFilter && workspaceId is null;
        var cacheKey = $"chatbots:{tenantId}:p{query.Page}:s{query.PageSize}";

        if (cacheable)
        {
            var cached = await cache.GetAsync<PagedResult<ChatbotDto>>(cacheKey, cancellationToken);
            if (cached is not null) return cached;
        }

        var filtered = await chatbotRepo.FindAsync(
            c => c.TenantId == tenantId && !c.IsDeleted
              && (!hasAssistantFilter || c.AssistantId == query.AssistantId)
              && (workspaceId == null || c.WorkspaceId == workspaceId), cancellationToken);

        var ordered = filtered.OrderByDescending(c => c.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        var result = new PagedResult<ChatbotDto>
        {
            Items = mapper.Map<List<ChatbotDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };

        if (cacheable)
            await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);
        return result;
    }
}
