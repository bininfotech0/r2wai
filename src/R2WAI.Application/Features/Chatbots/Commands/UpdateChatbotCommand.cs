using FluentValidation;

namespace R2WAI.Application.Features.Chatbots.Commands;

public record UpdateChatbotCommand : IRequest<ChatbotDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? WelcomeMessage { get; init; }
    public string? SuggestedQuestions { get; init; }
    public string? PromptTemplate { get; init; }
    public bool VoiceEnabled { get; init; }
}

public class UpdateChatbotCommandValidator : AbstractValidator<UpdateChatbotCommand>
{
    public UpdateChatbotCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpdateChatbotCommandHandler(
    IRepository<Chatbot> chatbotRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICacheService cacheService,
    IMapper mapper) : IRequestHandler<UpdateChatbotCommand, ChatbotDto>
{
    public async Task<ChatbotDto> Handle(UpdateChatbotCommand command, CancellationToken cancellationToken)
    {
        var chatbot = await chatbotRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Chatbot), command.Id);

        chatbot.UpdateDetails(command.Name, command.Description, command.WelcomeMessage,
            command.SuggestedQuestions, command.PromptTemplate);
        chatbot.SetVoiceEnabled(command.VoiceEnabled);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            for (var p = 1; p <= 5; p++)
                await cacheService.RemoveAsync($"chatbots:{tenantId}:p{p}:s20", cancellationToken);
        }

        return mapper.Map<ChatbotDto>(chatbot);
    }
}
