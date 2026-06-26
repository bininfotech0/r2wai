using FluentValidation;

namespace R2WAI.Application.Features.Chatbots.Commands;

public record DeleteChatbotCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteChatbotCommandValidator : AbstractValidator<DeleteChatbotCommand>
{
    public DeleteChatbotCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class DeleteChatbotCommandHandler(
    IRepository<Chatbot> chatbotRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICacheService cacheService) : IRequestHandler<DeleteChatbotCommand, Unit>
{
    public async Task<Unit> Handle(DeleteChatbotCommand command, CancellationToken cancellationToken)
    {
        var chatbot = await chatbotRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Chatbot), command.Id);

        chatbot.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            for (var p = 1; p <= 5; p++)
                await cacheService.RemoveAsync($"chatbots:{tenantId}:p{p}:s20", cancellationToken);
        }

        return Unit.Value;
    }
}
