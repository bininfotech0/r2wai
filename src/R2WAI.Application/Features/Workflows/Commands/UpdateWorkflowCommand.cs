using FluentValidation;

namespace R2WAI.Application.Features.Workflows.Commands;

public record UpdateWorkflowCommand : IRequest<WorkflowDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Type { get; init; }
    public string? Trigger { get; init; }
    public string? Steps { get; init; }
}

public class UpdateWorkflowCommandValidator : AbstractValidator<UpdateWorkflowCommand>
{
    public UpdateWorkflowCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpdateWorkflowCommandHandler(
    IRepository<Workflow> workflowRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateWorkflowCommand, WorkflowDto>
{
    public async Task<WorkflowDto> Handle(UpdateWorkflowCommand command, CancellationToken cancellationToken)
    {
        var workflow = await workflowRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Workflow), command.Id);

        workflow.UpdateDetails(command.Name, command.Description, command.Type,
            command.Trigger, command.Steps);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<WorkflowDto>(workflow);
    }
}
