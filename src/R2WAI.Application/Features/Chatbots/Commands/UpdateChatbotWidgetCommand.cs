using FluentValidation;

namespace R2WAI.Application.Features.Chatbots.Commands;

// Closes docs/api/MISSING-BACKEND-ENDPOINTS.md §3.1 (#50/#51): Chatbot.EmbedScript/WidgetSettings
// were real columns with a real domain setter (UpdateWidget) but zero callers — the widget
// deployment page built the embed script and appearance config client-side only, on every visit,
// with no server-side persistence. That's still the right *source of truth* for the snippet's
// data-* attributes (the shipped widget bundle reads them off the tag, not from an API), but the
// admin's chosen appearance couldn't survive a second visit or a second device before this.
public record UpdateChatbotWidgetCommand : IRequest<ChatbotDto>
{
    public Guid Id { get; init; }
    public string EmbedScript { get; init; } = string.Empty;
    public string WidgetSettings { get; init; } = string.Empty;
}

public class UpdateChatbotWidgetCommandValidator : AbstractValidator<UpdateChatbotWidgetCommand>
{
    public UpdateChatbotWidgetCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.EmbedScript).NotEmpty();
        RuleFor(v => v.WidgetSettings).NotEmpty();
    }
}

public class UpdateChatbotWidgetCommandHandler(
    IRepository<Chatbot> chatbotRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICacheService cacheService,
    IMapper mapper) : IRequestHandler<UpdateChatbotWidgetCommand, ChatbotDto>
{
    public async Task<ChatbotDto> Handle(UpdateChatbotWidgetCommand command, CancellationToken cancellationToken)
    {
        var chatbot = await chatbotRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Chatbot), command.Id);

        chatbot.UpdateWidget(command.EmbedScript, command.WidgetSettings);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            // Same cache-key convention as UpdateChatbotCommandHandler — GetById/GetList both
            // cache under these keys, and this write bypasses both.
            await Task.WhenAll(Enumerable.Range(1, 5)
                .Select(p => cacheService.RemoveAsync($"chatbots:{tenantId}:p{p}:s20", cancellationToken)));
        }

        return mapper.Map<ChatbotDto>(chatbot);
    }
}
