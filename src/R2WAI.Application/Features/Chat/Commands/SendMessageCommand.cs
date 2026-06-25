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
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IAIService aiService,
    IStreamingNotificationService streamingService,
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
            // Build conversation history for context
            var history = string.Join("\n", conversation.Messages
                .OrderBy(m => m.CreatedAt)
                .TakeLast(10)
                .Select(m => $"{(m.Role == MessageRole.User ? "User" : "Assistant")}: {m.Content}"));

            var responseBuffer = new System.Text.StringBuilder();
            
            await foreach (var chunk in aiService.StreamChatAsync(command.Content, history, null, cancellationToken))
            {
                responseBuffer.Append(chunk);
                await streamingService.SendStreamChunkAsync(command.ConversationId, chunk, cancellationToken);
            }

            var aiResponse = responseBuffer.ToString();
            var assistantMessage = conversation.AddMessage(
                Guid.NewGuid(), userMessage.Id, MessageRole.Assistant, aiResponse);
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
            throw;
        }
    }
}
