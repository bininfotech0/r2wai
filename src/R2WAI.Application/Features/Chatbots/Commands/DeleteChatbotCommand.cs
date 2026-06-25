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
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteChatbotCommand, Unit>
{
    public async Task<Unit> Handle(DeleteChatbotCommand command, CancellationToken cancellationToken)
    {
        var chatbot = await chatbotRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Chatbot), command.Id);

        chatbot.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
