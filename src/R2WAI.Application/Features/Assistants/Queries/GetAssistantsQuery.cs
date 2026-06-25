namespace R2WAI.Application.Features.Assistants.Queries;

public record GetAssistantsQuery : IRequest<PagedResult<AssistantDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
}

public class GetAssistantsQueryHandler(
    IRepository<AssistantDefinition> assistantRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetAssistantsQuery, PagedResult<AssistantDto>>
{
    public async Task<PagedResult<AssistantDto>> Handle(GetAssistantsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var searchTerm = query.Search?.ToLower();
        var filtered = await assistantRepo.FindAsync(
            a => a.TenantId == tenantId && !a.IsDeleted
              && (string.IsNullOrEmpty(searchTerm) || a.Name.ToLower().Contains(searchTerm)),
            cancellationToken);

        var ordered = filtered.OrderByDescending(a => a.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<AssistantDto>
        {
            Items = mapper.Map<List<AssistantDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
