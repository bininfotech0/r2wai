using FluentValidation;

namespace R2WAI.Application.Features.Chat.Commands;

public record SendMessageCommand : IRequest<MessageDto>
{
    public Guid ConversationId { get; init; }
    public string Content { get; init; } = string.Empty;
    public List<MessageAttachmentDto>? Attachments { get; init; }
    public string? IdempotencyKey { get; init; }
}

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(v => v.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");
        RuleFor(v => v.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(50000).WithMessage("Message must not exceed 50000 characters.");
    }
}

public class SendMessageCommandHandler(
    IRepository<Conversation> conversationRepo,
    IRepository<Message> messageRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IAIService aiService,
    IStreamingNotificationService streamingService,
    IChatStreamContext chatStreamContext,
    IConversationMemoryService conversationMemory,
    IStorageService storageService,
    IMapper mapper,
    IIdempotencyStore idempotencyStore,
    ILogger<SendMessageCommandHandler> logger) : IRequestHandler<SendMessageCommand, MessageDto>
{
    public async Task<MessageDto> Handle(SendMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!string.IsNullOrEmpty(command.IdempotencyKey))
        {
            var cached = await idempotencyStore.GetAsync<MessageDto>(command.IdempotencyKey, cancellationToken);
            if (cached is not null)
                return cached;
        }

        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var conversation = await conversationRepo.GetByIdAsync(command.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), command.ConversationId);

        if (conversation.IsDeleted || conversation.IsArchived)
            throw new UnauthorizedException("Conversation is archived or deleted.");

        var userMessage = conversation.AddMessage(
            Guid.NewGuid(), null, MessageRole.User, command.Content);
        await messageRepo.AddAsync(userMessage, cancellationToken);

        if (command.Attachments?.Count > 0)
        {
            foreach (var attachment in command.Attachments)
            {
                if (string.IsNullOrEmpty(attachment.TempFilePath) || !File.Exists(attachment.TempFilePath))
                    continue;

                try
                {
                    await using var fileStream = new FileStream(attachment.TempFilePath, FileMode.Open, FileAccess.Read);
                    var storagePath = await storageService.UploadFileAsync(
                        fileStream, attachment.FileName, attachment.ContentType, 
                        $"tenants/{tenantId}/chat/{conversation.Id}", cancellationToken);

                    userMessage.AddAttachment(new MessageAttachment(
                        Guid.NewGuid(), userMessage.Id, attachment.FileName,
                        storagePath, attachment.ContentType, attachment.FileSize));

                    // Clean up temp file
                    try { File.Delete(attachment.TempFilePath); } catch { /* ignore */ }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to upload attachment {FileName} for message {MessageId}", 
                        attachment.FileName, userMessage.Id);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            // Build conversation history for context — was previously an inline TakeLast(10) join
            // that bypassed IConversationMemoryService entirely, meaning Phase 6's summarization
            // feature (AI:ContextMemory:SummarizationEnabled) could never actually fire from this,
            // the real live chat entry point behind useChatSession. Fixed to route through the same
            // seam ChatService.SendMessageAsync already used (that class is being retired as dead
            // code — this is now the one real caller).
            var history = await conversationMemory.BuildConversationContextAsync(command.ConversationId, cancellationToken);

            var responseBuffer = new System.Text.StringBuilder();

            chatStreamContext.OnProgress = evt => evt.Kind == ToolCallProgressKind.Started
                ? streamingService.SendToolCallStartedAsync(command.ConversationId, evt.ToolName, cancellationToken)
                : streamingService.SendToolCallCompletedAsync(command.ConversationId, evt.ToolName, evt.Success ?? true, cancellationToken);

            await foreach (var chunk in aiService.StreamChatAsync(command.Content, history, null, enableTools: true, ct: cancellationToken))
            {
                responseBuffer.Append(chunk);
                await streamingService.SendStreamChunkAsync(command.ConversationId, chunk, cancellationToken);
            }

            var aiResponse = responseBuffer.ToString();
            var assistantMessage = conversation.AddMessage(
                Guid.NewGuid(), userMessage.Id, MessageRole.Assistant, aiResponse,
                contentBlocks: chatStreamContext.CapturedContentBlock);
            await messageRepo.AddAsync(assistantMessage, cancellationToken);
            assistantMessage.UpdateStatus(MessageStatus.Completed);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            await streamingService.SendStreamCompleteAsync(command.ConversationId, cancellationToken);

            var result = mapper.Map<MessageDto>(assistantMessage);

            if (!string.IsNullOrEmpty(command.IdempotencyKey))
                await idempotencyStore.SetAsync(command.IdempotencyKey, result, ct: cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI processing failed for message {MessageId}", userMessage.Id);
            userMessage.UpdateStatus(MessageStatus.Failed);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Persisting Failed status above is durable but silent — the client actively watching
            // this conversation's SignalR group was mid-stream and would otherwise just stop
            // receiving chunks with no explanation at all. Best-effort: a failure to notify must
            // never mask or replace the real exception below.
            try
            {
                await streamingService.SendStreamErrorAsync(command.ConversationId, "The AI service failed while generating a response. Please try again.", cancellationToken);
            }
            catch (Exception notifyEx)
            {
                logger.LogWarning(notifyEx, "Failed to notify conversation {ConversationId} of the stream error", command.ConversationId);
            }

            throw;
        }
    }
}
