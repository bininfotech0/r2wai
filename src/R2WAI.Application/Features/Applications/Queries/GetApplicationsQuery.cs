namespace R2WAI.Application.Features.Applications.Queries;

public record GetApplicationsQuery : IRequest<PagedResult<ApplicationDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid? DepartmentId { get; init; }
}

public class GetApplicationsQueryHandler(
    IRepository<ConnectedApplication> applicationRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetApplicationsQuery, PagedResult<ApplicationDto>>
{
    public async Task<PagedResult<ApplicationDto>> Handle(GetApplicationsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var searchTerm = query.Search?.ToLower();
        var filtered = await applicationRepo.FindAsync(
            a => a.TenantId == tenantId && !a.IsDeleted
              && (!query.DepartmentId.HasValue || a.DepartmentId == query.DepartmentId)
              && (string.IsNullOrEmpty(searchTerm)
                  || a.Name.ToLower().Contains(searchTerm)
                  || a.Code.ToLower().Contains(searchTerm)),
            cancellationToken);

        var ordered = filtered.OrderByDescending(a => a.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<ApplicationDto>
        {
            Items = mapper.Map<List<ApplicationDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
