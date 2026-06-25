namespace R2WAI.Application.Features.Workflows.Queries;

public record GetWorkflowsQuery : IRequest<PagedResult<WorkflowDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
}

public class GetWorkflowsQueryHandler(
    IRepository<Workflow> workflowRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetWorkflowsQuery, PagedResult<WorkflowDto>>
{
    public async Task<PagedResult<WorkflowDto>> Handle(GetWorkflowsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var searchTerm = query.Search?.ToLower();

        var filtered = await workflowRepo.FindAsync(
            w => w.TenantId == tenantId && !w.IsDeleted
              && (string.IsNullOrEmpty(searchTerm) || w.Name.ToLower().Contains(searchTerm)),
            cancellationToken);

        var ordered = filtered.OrderByDescending(w => w.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<WorkflowDto>
        {
            Items = mapper.Map<List<WorkflowDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
