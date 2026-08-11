namespace R2WAI.Application.Features.Applications.Queries;

public record GetDepartmentsQuery : IRequest<PagedResult<DepartmentDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
}

public class GetDepartmentsQueryHandler(
    IRepository<Department> departmentRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetDepartmentsQuery, PagedResult<DepartmentDto>>
{
    public async Task<PagedResult<DepartmentDto>> Handle(GetDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var searchTerm = query.Search?.ToLower();
        var filtered = await departmentRepo.FindAsync(
            d => d.TenantId == tenantId && !d.IsDeleted
              && (string.IsNullOrEmpty(searchTerm)
                  || d.Name.ToLower().Contains(searchTerm)
                  || d.Code.ToLower().Contains(searchTerm)),
            cancellationToken);

        var ordered = filtered.OrderBy(d => d.Name);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<DepartmentDto>
        {
            Items = mapper.Map<List<DepartmentDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
