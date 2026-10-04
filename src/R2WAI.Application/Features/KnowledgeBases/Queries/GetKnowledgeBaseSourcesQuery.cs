namespace R2WAI.Application.Features.KnowledgeBases.Queries;

/// <summary>
/// Flat, tenant-wide source list (docs/api/MISSING-BACKEND-ENDPOINTS.md §3.2 #57) — unlike
/// GetKnowledgeBaseByIdQuery, this isn't scoped to one knowledge base, so it can't rely on the
/// ambient tenant filter (KnowledgeBaseSource itself has no TenantId column); tenant scoping is
/// enforced explicitly through the parent KnowledgeBase.
/// </summary>
public record GetKnowledgeBaseSourcesQuery : IRequest<PagedResult<KnowledgeBaseSourceDto>>
{
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetKnowledgeBaseSourcesQueryHandler(
    IRepository<KnowledgeBaseSource> sourceRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetKnowledgeBaseSourcesQuery, PagedResult<KnowledgeBaseSourceDto>>
{
    public async Task<PagedResult<KnowledgeBaseSourceDto>> Handle(GetKnowledgeBaseSourcesQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var matches = await sourceRepo.FindAsync(
            s => s.KnowledgeBase.TenantId == tenantId && !s.KnowledgeBase.IsDeleted
              && (string.IsNullOrEmpty(query.Status) || s.Status == query.Status),
            "KnowledgeBase",
            cancellationToken);

        var ordered = matches.OrderByDescending(s => s.CreatedAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<KnowledgeBaseSourceDto>
        {
            Items = mapper.Map<List<KnowledgeBaseSourceDto>>(items),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }
}
