namespace R2WAI.Application.Features.Workflows.Commands;

public record AssignWorkflowApplicationCommand : IRequest<WorkflowDto>
{
    public Guid Id { get; init; }
    public Guid? ApplicationId { get; init; }
}

public class AssignWorkflowApplicationCommandHandler(
    IRepository<Workflow> workflowRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<AssignWorkflowApplicationCommand, WorkflowDto>
{
    public async Task<WorkflowDto> Handle(AssignWorkflowApplicationCommand command, CancellationToken cancellationToken)
    {
        var workflow = await workflowRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Workflow), command.Id);

        workflow.AssignApplication(command.ApplicationId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<WorkflowDto>(workflow);
    }
}
