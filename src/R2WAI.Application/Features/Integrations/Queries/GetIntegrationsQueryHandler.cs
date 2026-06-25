namespace R2WAI.Application.Features.Integrations.Queries;

public class GetIntegrationsQueryHandler(
    IRepository<ToolDefinition> repo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetIntegrationsQuery, PagedResult<IntegrationDto>>
{
    public async Task<PagedResult<IntegrationDto>> Handle(GetIntegrationsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var all = await repo.FindAsync(
            t => t.TenantId == tenantId, cancellationToken);

        IEnumerable<ToolDefinition> filtered = all;

        if (!string.IsNullOrEmpty(query.Search))
            filtered = filtered.Where(t => t.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(query.Category) && Enum.TryParse<ToolType>(query.Category, true, out var toolType))
            filtered = filtered.Where(t => t.ToolType == toolType);

        var list = filtered.OrderByDescending(t => t.CreatedAt).ToList();
        var total = list.Count;
        var items = list.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<IntegrationDto>
        {
            Items = mapper.Map<List<IntegrationDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
