using FluentValidation;

namespace R2WAI.Application.Features.Assistants.Commands;

public record DeleteAssistantCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteAssistantCommandValidator : AbstractValidator<DeleteAssistantCommand>
{
    public DeleteAssistantCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class DeleteAssistantCommandHandler(
    IRepository<AssistantDefinition> assistantRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICacheService cacheService) : IRequestHandler<DeleteAssistantCommand, Unit>
{
    public async Task<Unit> Handle(DeleteAssistantCommand command, CancellationToken cancellationToken)
    {
        var assistant = await assistantRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.Id);

        assistant.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            for (var p = 1; p <= 5; p++)
                await cacheService.RemoveAsync($"assistants:{tenantId}:p{p}:s20", cancellationToken);
        }

        return Unit.Value;
    }
}
