using R2WAI.Application.Common.Security;
using R2WAI.Application.Features.Tenants.DTOs;

namespace R2WAI.Application.Features.Tenants.Queries;

// Platform-wide, deliberately not tenant-scoped: Tenant itself has no TenantId property (it IS a
// tenant, see the fail-closed filter's own reflective "any entity with a TenantId property" scope
// in ApplicationDbContext.OnModelCreating), so a plain FindAsync here correctly returns every
// tenant, gated only by RequiredRoles below. SystemAdmin only — narrower than the Admin,SystemAdmin
// default most Admin queries use, matching docs/api/MISSING-BACKEND-ENDPOINTS.md §3.4 #67's own
// "SuperAdmin" callout for platform tenant management.
public record GetTenantsQuery : IRequest<PagedResult<TenantDto>>, IAuthorizedRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string[] RequiredRoles => ["SystemAdmin"];
}

public class GetTenantsQueryHandler(
    IRepository<Tenant> tenantRepo,
    IMapper mapper) : IRequestHandler<GetTenantsQuery, PagedResult<TenantDto>>
{
    public async Task<PagedResult<TenantDto>> Handle(GetTenantsQuery query, CancellationToken cancellationToken)
    {
        var searchTerm = query.Search?.ToLower();

        var filtered = await tenantRepo.FindAsync(
            t => string.IsNullOrEmpty(searchTerm)
                 || t.Name.ToLower().Contains(searchTerm)
                 || t.Slug.ToLower().Contains(searchTerm),
            cancellationToken);

        var ordered = filtered.OrderByDescending(t => t.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<TenantDto>
        {
            Items = mapper.Map<List<TenantDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}

public record GetTenantByIdQuery : IRequest<TenantDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string[] RequiredRoles => ["SystemAdmin"];
}

public class GetTenantByIdQueryHandler(
    IRepository<Tenant> tenantRepo,
    IMapper mapper) : IRequestHandler<GetTenantByIdQuery, TenantDto>
{
    public async Task<TenantDto> Handle(GetTenantByIdQuery query, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepo.GetByIdAsync(query.Id, cancellationToken);
        if (tenant is null || tenant.IsDeleted)
            throw new NotFoundException(nameof(Tenant), query.Id);

        return mapper.Map<TenantDto>(tenant);
    }
}
