using FluentValidation;

namespace R2WAI.Application.Features.Workflows.Commands;

public record BulkDeleteWorkflowsCommand : IRequest<Unit>
{
    public Guid[] Ids { get; init; } = [];
}

public class BulkDeleteWorkflowsCommandValidator : AbstractValidator<BulkDeleteWorkflowsCommand>
{
    public BulkDeleteWorkflowsCommandValidator()
    {
        RuleFor(v => v.Ids).NotEmpty();
    }
}

public class BulkDeleteWorkflowsCommandHandler(
    IRepository<Workflow> workflowRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<BulkDeleteWorkflowsCommand, Unit>
{
    public async Task<Unit> Handle(BulkDeleteWorkflowsCommand command, CancellationToken cancellationToken)
    {
        foreach (var id in command.Ids)
        {
            var workflow = await workflowRepo.GetByIdAsync(id, cancellationToken);
            workflow?.SoftDelete();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
