namespace R2WAI.Application.Features.Chatbots.Commands;

public record SetChatbotStatusCommand : IRequest<ChatbotDto>
{
    public Guid Id { get; init; }
    public ChatbotStatus Status { get; init; }
}

public class SetChatbotStatusCommandHandler(
    IRepository<Chatbot> chatbotRepo,
    IRepository<AssistantVersion> assistantVersionRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICacheService cacheService,
    IMapper mapper) : IRequestHandler<SetChatbotStatusCommand, ChatbotDto>
{
    public async Task<ChatbotDto> Handle(SetChatbotStatusCommand command, CancellationToken cancellationToken)
    {
        var chatbot = await chatbotRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Chatbot), command.Id);

        chatbot.UpdateStatus(command.Status);

        // Record which AssistantVersion was actually live at the moment of publish — provenance
        // only, does not change what Chat/StreamChat serve (see Chatbot.RecordPublishedAssistantVersion).
        if (command.Status == ChatbotStatus.Active && chatbot.AssistantId is { } assistantId)
        {
            var versions = await assistantVersionRepo.FindAsync(
                v => v.AssistantDefinitionId == assistantId && v.IsPublished, cancellationToken);
            var published = versions.FirstOrDefault();
            if (published is not null)
                chatbot.RecordPublishedAssistantVersion(published.Id, published.VersionNumber, DateTime.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            // Concurrent, not sequential -- see AssistantCacheKeys.InvalidateAsync for why.
            await Task.WhenAll(Enumerable.Range(1, 5)
                .Select(p => cacheService.RemoveAsync($"chatbots:{tenantId}:p{p}:s20", cancellationToken)));
        }

        return mapper.Map<ChatbotDto>(chatbot);
    }
}
