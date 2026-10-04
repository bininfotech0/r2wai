namespace R2WAI.Application.Features.Capabilities.Queries;

public record GetCapabilitiesQuery : IRequest<PagedResult<CapabilityDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid? ApplicationId { get; init; }
}

public class GetCapabilitiesQueryHandler(
    IRepository<ToolDefinition> capabilityRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetCapabilitiesQuery, PagedResult<CapabilityDto>>
{
    public async Task<PagedResult<CapabilityDto>> Handle(GetCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var searchTerm = query.Search?.ToLower();

        var filtered = await capabilityRepo.FindAsync(
            t => t.TenantId == tenantId && !t.IsDeleted
              && (string.IsNullOrEmpty(searchTerm) || t.Name.ToLower().Contains(searchTerm))
              && (!query.ApplicationId.HasValue || t.ApplicationId == query.ApplicationId),
            "ApplicationApi",
            cancellationToken);

        var ordered = filtered.OrderByDescending(t => t.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<CapabilityDto>
        {
            Items = mapper.Map<List<CapabilityDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
