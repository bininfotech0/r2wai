using FluentValidation;

namespace R2WAI.Application.Features.Chatbots.Commands;

public record ConnectChatbotChannelCommand : IRequest<Unit>
{
    public Guid ChatbotId { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
}

public class ConnectChatbotChannelCommandValidator : AbstractValidator<ConnectChatbotChannelCommand>
{
    public ConnectChatbotChannelCommandValidator()
    {
        RuleFor(v => v.ChatbotId).NotEmpty();
        RuleFor(v => v.Channel).NotEmpty()
            .Must(c => Enum.TryParse<ChatbotChannelType>(c, true, out _))
            .WithMessage("Unknown channel type.");
        RuleFor(v => v.PayloadJson).NotEmpty();
    }
}

public class ConnectChatbotChannelCommandHandler(
    IRepository<Chatbot> chatbotRepo,
    IRepository<ChatbotChannel> channelRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService) : IRequestHandler<ConnectChatbotChannelCommand, Unit>
{
    public async Task<Unit> Handle(ConnectChatbotChannelCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        _ = await chatbotRepo.GetByIdAsync(command.ChatbotId, cancellationToken)
            ?? throw new NotFoundException(nameof(Chatbot), command.ChatbotId);

        var channelType = Enum.Parse<ChatbotChannelType>(command.Channel, true);
        var encrypted = encryptionService.Encrypt(command.PayloadJson);

        var existing = await channelRepo.FirstOrDefaultAsync(
            c => c.ChatbotId == command.ChatbotId && c.ChannelType == channelType, cancellationToken);

        if (existing is not null)
        {
            existing.Connect(encrypted);
        }
        else
        {
            var channel = new ChatbotChannel(Guid.NewGuid(), tenantId, command.ChatbotId, channelType);
            channel.Connect(encrypted);
            await channelRepo.AddAsync(channel, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
