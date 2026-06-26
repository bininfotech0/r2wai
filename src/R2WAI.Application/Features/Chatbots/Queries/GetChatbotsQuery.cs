namespace R2WAI.Application.Features.Chatbots.Queries;

public record GetChatbotsQuery : IRequest<PagedResult<ChatbotDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetChatbotsQueryHandler(
    IRepository<Chatbot> chatbotRepo,
    ICurrentUserService currentUser,
    ICacheService cache,
    IMapper mapper) : IRequestHandler<GetChatbotsQuery, PagedResult<ChatbotDto>>
{
    public async Task<PagedResult<ChatbotDto>> Handle(GetChatbotsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var cacheKey = $"chatbots:{tenantId}:p{query.Page}:s{query.PageSize}";

        var cached = await cache.GetAsync<PagedResult<ChatbotDto>>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var filtered = await chatbotRepo.FindAsync(
            c => c.TenantId == tenantId && !c.IsDeleted, cancellationToken);

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

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);
        return result;
    }
}
