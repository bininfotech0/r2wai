using FluentValidation;

namespace R2WAI.Application.Features.Workflows.Commands;

public record DeleteWorkflowCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteWorkflowCommandValidator : AbstractValidator<DeleteWorkflowCommand>
{
    public DeleteWorkflowCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class DeleteWorkflowCommandHandler(
    IRepository<Workflow> workflowRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteWorkflowCommand, Unit>
{
    public async Task<Unit> Handle(DeleteWorkflowCommand command, CancellationToken cancellationToken)
    {
        var workflow = await workflowRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Workflow), command.Id);

        workflow.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
