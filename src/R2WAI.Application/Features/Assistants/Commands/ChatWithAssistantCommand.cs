using FluentValidation;

namespace R2WAI.Application.Features.Assistants.Commands;

public record ChatWithAssistantCommand : IRequest<ChatWithAssistantResult>
{
    public Guid AssistantId { get; init; }
    public string Message { get; init; } = string.Empty;
    public Guid? ConversationId { get; init; }
}

public record ChatWithAssistantResult(
    Guid ConversationId,
    string Reply,
    int TokensUsed,
    List<CitationDto>? Citations = null);

public record CitationDto(string SourceName, string Content, float Score, int Index);

public class ChatWithAssistantCommandValidator : AbstractValidator<ChatWithAssistantCommand>
{
    public ChatWithAssistantCommandValidator()
    {
        RuleFor(v => v.AssistantId).NotEmpty();
        RuleFor(v => v.Message).NotEmpty().MaximumLength(10000);
    }
}

public class ChatWithAssistantCommandHandler(
    IRepository<AssistantDefinition> assistantRepo,
    IRepository<Conversation> conversationRepo,
    IRepository<KnowledgeBase> kbRepo,
    IKnowledgeBaseService knowledgeBaseService,
    IAIService aiService,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    ILogger<ChatWithAssistantCommandHandler> logger) : IRequestHandler<ChatWithAssistantCommand, ChatWithAssistantResult>
{
    public async Task<ChatWithAssistantResult> Handle(ChatWithAssistantCommand command, CancellationToken cancellationToken)
    {
        var assistant = await assistantRepo.GetByIdAsync(command.AssistantId, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.AssistantId);

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        Conversation conversation;
        if (command.ConversationId.HasValue)
        {
            conversation = await conversationRepo.GetByIdAsync(command.ConversationId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Conversation), command.ConversationId.Value);
        }
        else
        {
            conversation = new Conversation(
                Guid.NewGuid(), tenantId, userId,
                $"Chat with {assistant.Name}",
                "assistant",
                assistant.Id);
            await conversationRepo.AddAsync(conversation, cancellationToken);
        }

        conversation.AddMessage(Guid.NewGuid(), null, MessageRole.User, command.Message);

        string? context = null;
        List<CitationDto>? citations = null;
        if (assistant.KnowledgeBaseId.HasValue)
        {
            try
            {
                var searchResult = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                    assistant.KnowledgeBaseId.Value, command.Message, 1, 5, cancellationToken);

                if (searchResult.Items.Count > 0)
                {
                    context = string.Join("\n\n", searchResult.Items.Select(i => i.Content));
                    citations = searchResult.Items
                        .Select((item, index) => new CitationDto(
                            item.SourceName ?? "Unknown",
                            item.Content,
                            (float)item.Score,
                            index + 1))
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to search knowledge base {KBId} for assistant {AssistantId}",
                    assistant.KnowledgeBaseId.Value, assistant.Id);
            }
        }

        var systemPrompt = assistant.SystemPrompt ?? "You are a helpful AI assistant.";
        var reply = await aiService.ChatAsync(
            command.Message,
            context,
            systemPrompt,
            cancellationToken);

        conversation.AddMessage(Guid.NewGuid(), null, MessageRole.Assistant, reply);
        assistant.IncrementUsageCount();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChatWithAssistantResult(conversation.Id, reply, 0, citations);
    }
}
