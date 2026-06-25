using FluentValidation;

namespace R2WAI.Application.Features.Workflows.Commands;

public record CreateWorkflowCommand : IRequest<WorkflowDto>
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Type { get; init; }
    public string? Steps { get; init; }
}

public class CreateWorkflowCommandValidator : AbstractValidator<CreateWorkflowCommand>
{
    public CreateWorkflowCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
    }
}

public class CreateWorkflowCommandHandler(
    IRepository<Workflow> workflowRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateWorkflowCommand, WorkflowDto>
{
    public async Task<WorkflowDto> Handle(CreateWorkflowCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var workflow = new Workflow(
            Guid.NewGuid(), tenantId, userId, command.Name,
            command.Description, command.Type, command.Steps);

        await workflowRepo.AddAsync(workflow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<WorkflowDto>(workflow);
    }
}
