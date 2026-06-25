namespace R2WAI.Application.Features.Workflows.Queries;

public record GetWorkflowInstanceByIdQuery : IRequest<WorkflowInstanceDto>
{
    public Guid Id { get; init; }
}

public class GetWorkflowInstanceByIdQueryHandler(
    IRepository<WorkflowInstance> instanceRepo,
    IMapper mapper) : IRequestHandler<GetWorkflowInstanceByIdQuery, WorkflowInstanceDto>
{
    public async Task<WorkflowInstanceDto> Handle(GetWorkflowInstanceByIdQuery query, CancellationToken cancellationToken)
    {
        var instance = await instanceRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WorkflowInstance), query.Id);

        return mapper.Map<WorkflowInstanceDto>(instance);
    }
}
