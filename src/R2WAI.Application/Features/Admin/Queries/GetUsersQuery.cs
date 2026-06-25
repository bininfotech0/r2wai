namespace R2WAI.Application.Features.Admin.Queries;

public record GetUsersQuery : IRequest<PagedResult<UserDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetUsersQueryHandler(
    IRepository<User> userRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetUsersQuery, PagedResult<UserDto>>
{
    public async Task<PagedResult<UserDto>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var filtered = await userRepo.FindAsync(
            u => u.TenantId == tenantId && !u.IsDeleted, cancellationToken);

        var ordered = filtered.OrderByDescending(u => u.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<UserDto>
        {
            Items = mapper.Map<List<UserDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
