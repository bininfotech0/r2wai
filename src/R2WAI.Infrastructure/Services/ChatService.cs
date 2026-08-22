using System.Text;
using Microsoft.EntityFrameworkCore;

namespace R2WAI.Infrastructure.Services;

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;
    private readonly IAgentRuntime _agentRuntime;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IStreamingNotificationService _streaming;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConversationMemoryService _conversationMemory;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ApplicationDbContext context,
        IAgentRuntime agentRuntime,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService,
        IStreamingNotificationService streaming,
        IServiceScopeFactory scopeFactory,
        IConversationMemoryService conversationMemory,
        ILogger<ChatService> logger)
    {
        _context = context;
        _agentRuntime = agentRuntime;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
        _streaming = streaming;
        _scopeFactory = scopeFactory;
        _conversationMemory = conversationMemory;
        _logger = logger;
    }

    public async Task<ConversationDto> CreateConversationAsync(Guid tenantId, Guid userId, string title, string? module, Guid? referenceId, CancellationToken ct = default)
    {
        var conversation = new Conversation(Guid.NewGuid(), tenantId, userId, title, module, referenceId);
        await _context.Conversations.AddAsync(conversation, ct);
        await _context.SaveChangesAsync(ct);

        return MapToDto(conversation);
    }

    public async Task<PagedResult<ConversationDto>> GetConversationsAsync(Guid tenantId, Guid userId, int page, int pageSize, string? module, CancellationToken ct = default)
    {
        var query = _context.Conversations
            .Where(c => c.TenantId == tenantId && c.UserId == userId);

        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(c => c.Module == module);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.Messages.Any() ? c.Messages.Max(m => m.CreatedAt) : c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConversationDto
            {
                Id = c.Id,
                Title = c.Title,
                Module = c.Module,
                MessageCount = c.Messages.Count,
                LastMessageAt = c.Messages.Any() ? c.Messages.Max(m => m.CreatedAt) : null,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<ConversationDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ConversationDto> GetConversationByIdAsync(Guid id, CancellationToken ct = default)
    {
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversation is null)
            throw new NotFoundException(nameof(Conversation), id);

        return MapToDto(conversation);
    }

    public async Task DeleteConversationAsync(Guid id, CancellationToken ct = default)
    {
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversation is null)
            throw new NotFoundException(nameof(Conversation), id);

        conversation.Archive();
        await _context.SaveChangesAsync(ct);
    }

    public async Task<MessageDto> SendMessageAsync(Guid conversationId, Guid tenantId, Guid userId, string content, IReadOnlyList<MessageAttachmentDto>? attachments, CancellationToken ct = default)
    {
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
            throw new NotFoundException(nameof(Conversation), conversationId);

        var messageId = Guid.NewGuid();
        var piiTypes = PiiScanner.Scan(content);
        if (piiTypes.Count > 0)
        {
            _logger.LogWarning("Message {MessageId} in conversation {ConversationId} contains possible PII ({PiiTypes}) — may be sent to an external AI provider",
                messageId, conversationId, string.Join(", ", piiTypes));
        }
        var messageMetadata = piiTypes.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(new { possiblePii = piiTypes })
            : null;
        var message = conversation.AddMessage(messageId, null, MessageRole.User, content, metadata: messageMetadata);
        // Conversation was loaded (not newly Add()-ed), so it's tracked Unchanged: EF Core cannot tell
        // a client-generated-Guid child discovered only via navigation fixup is new rather than existing,
        // and defaults to Modified — which throws DbUpdateConcurrencyException (0 rows) on save. Adding
        // the message explicitly removes the ambiguity.
        _context.Messages.Add(message);

        if (attachments?.Count > 0)
        {
            foreach (var attachment in attachments)
            {
                var msgAttachment = new MessageAttachment(
                    Guid.NewGuid(), messageId, attachment.FileName,
                    attachment.FileName, attachment.ContentType, attachment.FileSize);
                _context.MessageAttachments.Add(msgAttachment);
            }
        }

        message.AddDomainEvent(new MessageCreatedEvent(messageId, conversationId, tenantId, userId, content));

        await _context.SaveChangesAsync(ct);

        var history = await _conversationMemory.BuildConversationContextAsync(conversationId, ct);

        var responseBuffer = new StringBuilder();
        var responseMessageId = Guid.NewGuid();
        var conversationGroup = $"conversation_{conversationId}";

        await foreach (var chunk in _agentRuntime.StreamAsync(content, history, null, ct))
        {
            responseBuffer.Append(chunk);
            await _streaming.SendStreamChunkAsync(conversationId, chunk, ct);
        }

        var aiResponse = responseBuffer.ToString();

        // Saved via a fresh scope/DbContext rather than the ambient _context: the AI call above can run
        // for minutes, and reusing a DbContext whose connection has sat idle that long risks a stale
        // retry (EnableRetryOnFailure) producing a spurious DbUpdateConcurrencyException on this save.
        Message responseMessage;
        using (var scope = _scopeFactory.CreateScope())
        {
            var freshContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var freshConversation = await freshContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId, ct)
                ?? throw new NotFoundException(nameof(Conversation), conversationId);
            responseMessage = freshConversation.AddMessage(responseMessageId, messageId, MessageRole.Assistant, aiResponse);
            freshContext.Messages.Add(responseMessage);
            await freshContext.SaveChangesAsync(ct);
        }

        await _streaming.SendStreamCompleteAsync(conversationId, ct);

        return new MessageDto
        {
            Id = responseMessage.Id,
            Role = responseMessage.Role,
            Content = responseMessage.Content,
            ContentBlocks = responseMessage.ContentBlocks,
            Status = responseMessage.Status,
            Attachments = [],
            CreatedAt = responseMessage.CreatedAt
        };
    }

    public async Task<PagedResult<MessageDto>> GetMessagesAsync(Guid conversationId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Messages
            .Include(m => m.Attachments)
            .Where(m => m.ConversationId == conversationId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                ContentBlocks = m.ContentBlocks,
                Status = m.Status,
                Attachments = m.Attachments.Select(a => new MessageAttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSize = a.FileSize
                }).ToList(),
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<MessageDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<IReadOnlyList<SuggestedActionDto>> GetSuggestedActionsAsync(Guid? conversationId, CancellationToken ct = default)
    {
        var actions = new List<SuggestedActionDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Summarize", Description = "Summarize the conversation", Icon = "summarize" },
            new() { Id = Guid.NewGuid(), Title = "Generate Report", Description = "Generate a report from this conversation", Icon = "report" },
            new() { Id = Guid.NewGuid(), Title = "Export", Description = "Export conversation", Icon = "export" }
        };

        return Task.FromResult<IReadOnlyList<SuggestedActionDto>>(actions);
    }

    private static ConversationDto MapToDto(Conversation conversation)
    {
        return new ConversationDto
        {
            Id = conversation.Id,
            Title = conversation.Title,
            Module = conversation.Module,
            MessageCount = conversation.Messages?.Count ?? 0,
            LastMessageAt = conversation.Messages?.Any() == true
                ? conversation.Messages.Max(m => m.CreatedAt)
                : null,
            CreatedAt = conversation.CreatedAt
        };
    }
}
