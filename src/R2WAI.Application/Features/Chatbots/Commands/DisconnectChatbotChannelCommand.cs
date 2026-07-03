using FluentValidation;

namespace R2WAI.Application.Features.Chatbots.Commands;

public record DisconnectChatbotChannelCommand : IRequest<Unit>
{
    public Guid ChatbotId { get; init; }
    public string Channel { get; init; } = string.Empty;
}

public class DisconnectChatbotChannelCommandValidator : AbstractValidator<DisconnectChatbotChannelCommand>
{
    public DisconnectChatbotChannelCommandValidator()
    {
        RuleFor(v => v.ChatbotId).NotEmpty();
        RuleFor(v => v.Channel).NotEmpty()
            .Must(c => Enum.TryParse<ChatbotChannelType>(c, true, out _))
            .WithMessage("Unknown channel type.");
    }
}

public class DisconnectChatbotChannelCommandHandler(
    IRepository<ChatbotChannel> channelRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DisconnectChatbotChannelCommand, Unit>
{
    public async Task<Unit> Handle(DisconnectChatbotChannelCommand command, CancellationToken cancellationToken)
    {
        var channelType = Enum.Parse<ChatbotChannelType>(command.Channel, true);

        var channel = await channelRepo.FirstOrDefaultAsync(
            c => c.ChatbotId == command.ChatbotId && c.ChannelType == channelType, cancellationToken)
            ?? throw new NotFoundException(nameof(ChatbotChannel), command.ChatbotId);

        channel.Disconnect();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
