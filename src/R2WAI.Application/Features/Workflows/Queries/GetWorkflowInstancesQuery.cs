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
    IRepository<ConnectedApplication> applicationRepo,
    IRepository<User> userRepo,
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
        var workflowById = workflows.ToDictionary(w => w.Id);

        var applicationIds = workflows.Where(w => w.ApplicationId.HasValue).Select(w => w.ApplicationId!.Value).Distinct().ToList();
        var applicationNames = applicationIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await applicationRepo.FindAsync(a => applicationIds.Contains(a.Id), cancellationToken)).ToDictionary(a => a.Id, a => a.Name);

        var initiatorIds = items.Select(i => i.InitiatedBy).Distinct().ToList();
        var initiatorNames = initiatorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await userRepo.FindAsync(u => initiatorIds.Contains(u.Id), cancellationToken)).ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

        var initiatedByLookup = items.ToDictionary(i => i.Id, i => i.InitiatedBy);

        foreach (var dto in dtos)
        {
            if (workflowById.TryGetValue(dto.WorkflowId, out var workflow))
            {
                dto.WorkflowName = workflow.Name;
                dto.ApplicationId = workflow.ApplicationId;
                if (workflow.ApplicationId.HasValue && applicationNames.TryGetValue(workflow.ApplicationId.Value, out var appName))
                    dto.ApplicationName = appName;
            }
            if (initiatedByLookup.TryGetValue(dto.Id, out var initiatorId) && initiatorNames.TryGetValue(initiatorId, out var initiatorName))
            {
                dto.InitiatedByUserName = initiatorName;
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
