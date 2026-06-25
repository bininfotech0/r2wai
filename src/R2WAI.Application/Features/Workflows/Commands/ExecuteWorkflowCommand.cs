using FluentValidation;

namespace R2WAI.Application.Features.Workflows.Commands;

public record ExecuteWorkflowCommand : IRequest<WorkflowInstanceDto>, ITransactionalRequest
{
    public Guid WorkflowId { get; init; }
    public string? Data { get; init; }
}

public class ExecuteWorkflowCommandValidator : AbstractValidator<ExecuteWorkflowCommand>
{
    public ExecuteWorkflowCommandValidator()
    {
        RuleFor(v => v.WorkflowId).NotEmpty();
    }
}

public class ExecuteWorkflowCommandHandler(
    IRepository<Workflow> workflowRepo,
    IRepository<WorkflowInstance> instanceRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<ExecuteWorkflowCommand, WorkflowInstanceDto>
{
    public async Task<WorkflowInstanceDto> Handle(ExecuteWorkflowCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var workflow = await workflowRepo.GetByIdAsync(command.WorkflowId, cancellationToken)
            ?? throw new NotFoundException(nameof(Workflow), command.WorkflowId);

        if (!workflow.IsActive)
        {
            workflow.Activate();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var instance = new WorkflowInstance(
            Guid.NewGuid(), command.WorkflowId, tenantId, userId, command.Data);

        await instanceRepo.AddAsync(instance, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<WorkflowInstanceDto>(instance);
    }
}
