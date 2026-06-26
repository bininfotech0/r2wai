namespace R2WAI.Application.Features.Assistants.Queries;

public record GetAssistantsQuery : IRequest<PagedResult<AssistantDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
}

public class GetAssistantsQueryHandler(
    IRepository<AssistantDefinition> assistantRepo,
    ICurrentUserService currentUser,
    ICacheService cache,
    IMapper mapper) : IRequestHandler<GetAssistantsQuery, PagedResult<AssistantDto>>
{
    public async Task<PagedResult<AssistantDto>> Handle(GetAssistantsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var hasSearch = !string.IsNullOrEmpty(query.Search);
        if (!hasSearch)
        {
            var cacheKey = $"assistants:{tenantId}:p{query.Page}:s{query.PageSize}";
            var cached = await cache.GetAsync<PagedResult<AssistantDto>>(cacheKey, cancellationToken);
            if (cached is not null) return cached;
        }

        var searchTerm = query.Search?.ToLower();
        var filtered = await assistantRepo.FindAsync(
            a => a.TenantId == tenantId && !a.IsDeleted
              && (string.IsNullOrEmpty(searchTerm) || a.Name.ToLower().Contains(searchTerm)),
            cancellationToken);

        var ordered = filtered.OrderByDescending(a => a.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        var result = new PagedResult<AssistantDto>
        {
            Items = mapper.Map<List<AssistantDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };

        if (!hasSearch)
            await cache.SetAsync($"assistants:{tenantId}:p{query.Page}:s{query.PageSize}", result, TimeSpan.FromMinutes(2), cancellationToken);

        return result;
    }
}
