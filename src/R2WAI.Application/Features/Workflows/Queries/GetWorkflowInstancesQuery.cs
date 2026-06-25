namespace R2WAI.Application.Features.Workflows.Queries;

public record GetWorkflowInstancesQuery : IRequest<PagedResult<WorkflowInstanceDto>>
{
    public Guid? WorkflowId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetWorkflowInstancesQueryHandler(
    IRepository<WorkflowInstance> instanceRepo,
    IRepository<Workflow> workflowRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetWorkflowInstancesQuery, PagedResult<WorkflowInstanceDto>>
{
    public async Task<PagedResult<WorkflowInstanceDto>> Handle(GetWorkflowInstancesQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var all = await instanceRepo.FindAsync(
            i => i.TenantId == tenantId
              && (!query.WorkflowId.HasValue || i.WorkflowId == query.WorkflowId),
            cancellationToken);

        var ordered = all.OrderByDescending(i => i.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        var dtos = mapper.Map<List<WorkflowInstanceDto>>(items);

        var workflowIds = items.Select(i => i.WorkflowId).Distinct().ToList();
        var workflows = await workflowRepo.FindAsync(w => workflowIds.Contains(w.Id), cancellationToken);
        var workflowNames = workflows.ToDictionary(w => w.Id, w => w.Name);

        foreach (var dto in dtos)
        {
            if (workflowNames.TryGetValue(dto.WorkflowId, out var name))
            {
                dto.WorkflowName = name;
            }
        }

        return new PagedResult<WorkflowInstanceDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
