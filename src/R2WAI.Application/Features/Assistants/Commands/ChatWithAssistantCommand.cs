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
    List<CitationDto>? Citations = null,
    long DurationMs = 0,
    List<FunctionCallTraceDto>? FunctionCalls = null);

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
    IRepository<Message> messageRepo,
    IRepository<KnowledgeBase> kbRepo,
    IKnowledgeBaseService knowledgeBaseService,
    IAIService aiService,
    IPromptTemplateService promptTemplateService,
    IChatTraceCollector traceCollector,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    ILogger<ChatWithAssistantCommandHandler> logger) : IRequestHandler<ChatWithAssistantCommand, ChatWithAssistantResult>
{
    public async Task<ChatWithAssistantResult> Handle(ChatWithAssistantCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        traceCollector.Clear();
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

        var userMessage = conversation.AddMessage(Guid.NewGuid(), null, MessageRole.User, command.Message);
        // Existing conversations are loaded (tracked Unchanged), not Add()-ed: EF Core cannot tell a
        // client-generated-Guid child discovered only via navigation fixup is new rather than existing,
        // and defaults to Modified — which throws DbUpdateConcurrencyException (0 rows) on save.
        await messageRepo.AddAsync(userMessage, cancellationToken);

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

        var basePrompt = assistant.SystemPrompt
            ?? await promptTemplateService.GetActiveTemplateAsync(assistant.Type, tenantId, cancellationToken);
        var systemPrompt = basePrompt
            + $"\n\n[Assistant context: your assistant ID is {assistant.Id}, your name is \"{assistant.Name}\"" +
              (!string.IsNullOrWhiteSpace(assistant.Description)
                  ? $", and your purpose is: {assistant.Description}"
                  : $", and you are a {assistant.Type} assistant") +
              ". This is everything you need to know about yourself — you do not need to call get_assistant_context " +
              "to answer questions about what you do or who you are. If you need an assistant ID for some other tool, " +
              "use the ID above directly, never guess or leave it blank.]";
        var reply = await aiService.ChatAsync(
            command.Message,
            context,
            systemPrompt,
            enableTools: true,
            ct: cancellationToken);

        var assistantMessage = conversation.AddMessage(Guid.NewGuid(), null, MessageRole.Assistant, reply);
        await messageRepo.AddAsync(assistantMessage, cancellationToken);
        assistant.IncrementUsageCount();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        stopwatch.Stop();
        var functionCalls = traceCollector.GetTrace().ToList();

        return new ChatWithAssistantResult(conversation.Id, reply, 0, citations, stopwatch.ElapsedMilliseconds, functionCalls);
    }
}
