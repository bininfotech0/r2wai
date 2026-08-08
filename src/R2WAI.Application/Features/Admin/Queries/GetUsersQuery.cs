namespace R2WAI.Application.Features.Admin.Queries;

public record GetUsersQuery : IRequest<PagedResult<UserDto>>, IAuthorizedRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin", "UserManager"];
}

public class GetUsersQueryHandler(
    IRepository<User> userRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetUsersQuery, PagedResult<UserDto>>
{
    public async Task<PagedResult<UserDto>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var searchTerm = query.Search?.ToLower();

        var filtered = await userRepo.FindAsync(
            u => u.TenantId == tenantId && !u.IsDeleted
              && (string.IsNullOrEmpty(searchTerm)
                  || u.Email.ToLower().Contains(searchTerm)
                  || u.FirstName.ToLower().Contains(searchTerm)
                  || u.LastName.ToLower().Contains(searchTerm)),
            cancellationToken);

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

public record GetUserByIdQuery : IRequest<UserDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin", "UserManager"];
}

public class GetUserByIdQueryHandler(
    IRepository<User> userRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetUserByIdQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var user = await userRepo.FirstOrDefaultAsync(
            u => u.Id == query.Id && u.TenantId == tenantId && !u.IsDeleted,
            cancellationToken);

        if (user is null)
            throw new NotFoundException(nameof(User), query.Id);

        return mapper.Map<UserDto>(user);
    }
}
