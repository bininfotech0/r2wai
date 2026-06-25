namespace R2WAI.Application.Features.Documents.Queries;

public record GetDocumentsQuery : IRequest<PagedResult<DocumentDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? KnowledgeBaseId { get; init; }
}

public class GetDocumentsQueryHandler(
    IRepository<Document> documentRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetDocumentsQuery, PagedResult<DocumentDto>>
{
    public async Task<PagedResult<DocumentDto>> Handle(GetDocumentsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var filtered = await documentRepo.FindAsync(
            d => d.TenantId == tenantId && !d.IsDeleted
              && (!query.KnowledgeBaseId.HasValue || d.KnowledgeBaseId == query.KnowledgeBaseId),
            cancellationToken);

        var ordered = filtered.OrderByDescending(d => d.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<DocumentDto>
        {
            Items = mapper.Map<List<DocumentDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
