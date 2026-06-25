namespace R2WAI.Application.Features.Workflows.Queries;

public record GetWorkflowByIdQuery : IRequest<WorkflowDto>
{
    public Guid Id { get; init; }
}

public class GetWorkflowByIdQueryHandler(
    IRepository<Workflow> workflowRepo,
    IMapper mapper) : IRequestHandler<GetWorkflowByIdQuery, WorkflowDto>
{
    public async Task<WorkflowDto> Handle(GetWorkflowByIdQuery query, CancellationToken cancellationToken)
    {
        var workflow = await workflowRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Workflow), query.Id);

        if (workflow.IsDeleted)
            throw new NotFoundException(nameof(Workflow), query.Id);

        return mapper.Map<WorkflowDto>(workflow);
    }
}
