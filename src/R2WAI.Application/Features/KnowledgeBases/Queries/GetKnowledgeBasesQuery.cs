namespace R2WAI.Application.Features.KnowledgeBases.Queries;

public record GetKnowledgeBasesQuery : IRequest<PagedResult<KnowledgeBaseDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetKnowledgeBasesQueryHandler(
    IRepository<KnowledgeBase> kbRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetKnowledgeBasesQuery, PagedResult<KnowledgeBaseDto>>
{
    public async Task<PagedResult<KnowledgeBaseDto>> Handle(GetKnowledgeBasesQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var filtered = await kbRepo.FindAsync(
            kb => kb.TenantId == tenantId && !kb.IsDeleted, cancellationToken);

        var ordered = filtered.OrderByDescending(kb => kb.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<KnowledgeBaseDto>
        {
            Items = mapper.Map<List<KnowledgeBaseDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
