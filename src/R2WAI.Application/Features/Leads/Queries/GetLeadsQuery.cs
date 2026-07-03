namespace R2WAI.Application.Features.Leads.Queries;

public record GetLeadsQuery : IRequest<PagedResult<LeadDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public LeadStatus? Status { get; init; }
}

public class GetLeadsQueryHandler(
    IRepository<Lead> leadRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetLeadsQuery, PagedResult<LeadDto>>
{
    public async Task<PagedResult<LeadDto>> Handle(GetLeadsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var filtered = await leadRepo.FindAsync(
            l => l.TenantId == tenantId && !l.IsDeleted && (query.Status == null || l.Status == query.Status),
            cancellationToken);

        var ordered = filtered.OrderByDescending(l => l.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<LeadDto>
        {
            Items = mapper.Map<List<LeadDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
