using FluentValidation;

namespace R2WAI.Application.Features.Chatbots.Commands;

public record CreateChatbotCommand : IRequest<ChatbotDto>
{
    public string Name { get; init; } = string.Empty;
    public Guid? KnowledgeBaseId { get; init; }
    public Guid? ModelConfigurationId { get; init; }
}

public class CreateChatbotCommandValidator : AbstractValidator<CreateChatbotCommand>
{
    public CreateChatbotCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
    }
}

public class CreateChatbotCommandHandler(
    IRepository<Chatbot> chatbotRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateChatbotCommand, ChatbotDto>
{
    public async Task<ChatbotDto> Handle(CreateChatbotCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var chatbot = new Chatbot(
            Guid.NewGuid(), tenantId, userId, command.Name,
            command.KnowledgeBaseId, command.ModelConfigurationId);

        await chatbotRepo.AddAsync(chatbot, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ChatbotDto>(chatbot);
    }
}
