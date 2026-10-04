using R2WAI.Domain.Enums;

namespace R2WAI.Application.Features.Assistants.Queries;

public record GetAssistantsQuery : IRequest<AssistantsPagedResult>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid? ApplicationId { get; init; }
    public PublishStatus? PublishStatus { get; init; }
    // Mirrors the client's ASSISTANT_SORTS keys (assistantView.ts) exactly: "recent" (default),
    // "name", "usage", "least-used". Unrecognized/omitted values fall back to "recent".
    public string? SortBy { get; init; }
}

/// <summary>
/// Adds tenant-wide (well, tenant+search+applicationId-scoped — same scope as the paged items
/// below, just not narrowed by PublishStatus) per-status counts for the library's filter labels,
/// without changing the shape callers already parse (Items/TotalCount/Page/PageSize stay exactly
/// as PagedResult&lt;AssistantDto&gt; always returned them — StatusCounts is additive).
/// </summary>
public class AssistantsPagedResult : PagedResult<AssistantDto>
{
    public Dictionary<string, int> StatusCounts { get; init; } = [];
}

public class GetAssistantsQueryHandler(
    IRepository<AssistantDefinition> assistantRepo,
    ICurrentUserService currentUser,
    ICacheService cache,
    IMapper mapper) : IRequestHandler<GetAssistantsQuery, AssistantsPagedResult>
{
    public async Task<AssistantsPagedResult> Handle(GetAssistantsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var hasSearch = !string.IsNullOrEmpty(query.Search);
        var hasApplicationFilter = query.ApplicationId.HasValue;
        var hasStatusFilter = query.PublishStatus.HasValue;
        var hasSortOverride = !string.IsNullOrEmpty(query.SortBy) && query.SortBy != "recent";
        var cacheable = !hasSearch && !hasApplicationFilter && !hasStatusFilter && !hasSortOverride;

        if (cacheable)
        {
            var cacheKey = $"assistants:{tenantId}:p{query.Page}:s{query.PageSize}";
            var cached = await cache.GetAsync<AssistantsPagedResult>(cacheKey, cancellationToken);
            if (cached is not null) return cached;
        }

        var searchTerm = query.Search?.ToLower();
        var scoped = await assistantRepo.FindAsync(
            a => a.TenantId == tenantId && !a.IsDeleted
              && (string.IsNullOrEmpty(searchTerm) || a.Name.ToLower().Contains(searchTerm))
              && (!hasApplicationFilter || a.ApplicationId == query.ApplicationId),
            cancellationToken);

        // Counted over the search/applicationId-scoped set, before the PublishStatus filter below
        // narrows it further — so switching status tabs never has to re-fetch to keep every tab's
        // count accurate, and a search term correctly narrows the counts too.
        var statusCounts = scoped.GroupBy(a => a.PublishStatus).ToDictionary(g => g.Key.ToString(), g => g.Count());
        foreach (var status in Enum.GetValues<PublishStatus>())
            statusCounts.TryAdd(status.ToString(), 0);

        var filtered = hasStatusFilter
            ? scoped.Where(a => a.PublishStatus == query.PublishStatus!.Value)
            : scoped;

        var ordered = query.SortBy switch
        {
            "name" => filtered.OrderBy(a => a.Name),
            "usage" => filtered.OrderByDescending(a => a.UsageCount),
            "least-used" => filtered.OrderBy(a => a.UsageCount),
            _ => filtered.OrderByDescending(a => a.CreatedAt),
        };
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        var result = new AssistantsPagedResult
        {
            Items = mapper.Map<List<AssistantDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
            StatusCounts = statusCounts,
        };

        if (cacheable)
            await cache.SetAsync($"assistants:{tenantId}:p{query.Page}:s{query.PageSize}", result, TimeSpan.FromMinutes(2), cancellationToken);

        return result;
    }
}
