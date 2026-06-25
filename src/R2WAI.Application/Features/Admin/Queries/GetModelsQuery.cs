namespace R2WAI.Application.Features.Admin.Queries;

public record GetModelsQuery : IRequest<PagedResult<ModelConfigDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public class GetModelsQueryHandler(
    IRepository<ModelConfiguration> modelRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetModelsQuery, PagedResult<ModelConfigDto>>
{
    public async Task<PagedResult<ModelConfigDto>> Handle(GetModelsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var all = await modelRepo.FindAsync(
            m => m.TenantId == tenantId && m.IsActive, cancellationToken);
        var filtered = all.OrderByDescending(m => m.IsDefault)
                          .ThenBy(m => m.Name)
                          .ToList();

        var total = filtered.Count;
        var items = filtered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<ModelConfigDto>
        {
            Items = mapper.Map<List<ModelConfigDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
