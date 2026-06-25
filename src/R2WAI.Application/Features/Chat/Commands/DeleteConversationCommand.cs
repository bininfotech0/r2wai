using FluentValidation;

namespace R2WAI.Application.Features.Chat.Commands;

public record DeleteConversationCommand : IRequest<Unit>
{
    public Guid ConversationId { get; init; }
}

public class DeleteConversationCommandValidator : AbstractValidator<DeleteConversationCommand>
{
    public DeleteConversationCommandValidator()
    {
        RuleFor(v => v.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");
    }
}

public class DeleteConversationCommandHandler(
    IRepository<Conversation> conversationRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<DeleteConversationCommand, Unit>
{
    public async Task<Unit> Handle(DeleteConversationCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var conversation = await conversationRepo.GetByIdAsync(command.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), command.ConversationId);

        if (conversation.UserId != userId)
            throw new UnauthorizedException("You can only delete your own conversations.");

        conversation.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
