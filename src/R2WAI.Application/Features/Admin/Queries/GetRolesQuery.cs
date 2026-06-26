namespace R2WAI.Application.Features.Admin.Queries;

public record GetRolesQuery : IRequest<PagedResult<RoleDto>>, IAuthorizedRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class GetRolesQueryHandler(
    IRepository<Role> roleRepo,
    ICurrentUserService currentUser,
    IMapper mapper,
    Common.Interfaces.ICacheService cache) : IRequestHandler<GetRolesQuery, PagedResult<RoleDto>>
{
    public async Task<PagedResult<RoleDto>> Handle(GetRolesQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var cacheKey = $"roles:{tenantId}:p{query.Page}:s{query.PageSize}";

        var cached = await cache.GetAsync<PagedResult<RoleDto>>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var filtered = await roleRepo.FindAsync(
            r => r.TenantId == tenantId && !r.IsDeleted, cancellationToken);

        var ordered = filtered.OrderByDescending(r => r.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        var result = new PagedResult<RoleDto>
        {
            Items = mapper.Map<List<RoleDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
        return result;
    }
}
