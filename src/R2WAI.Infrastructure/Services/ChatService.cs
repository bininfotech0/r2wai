using System.Text;
using Microsoft.EntityFrameworkCore;

namespace R2WAI.Infrastructure.Services;

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IStreamingNotificationService _streaming;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ApplicationDbContext context,
        IAIService aiService,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService,
        IStreamingNotificationService streaming,
        ILogger<ChatService> logger)
    {
        _context = context;
        _aiService = aiService;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
        _streaming = streaming;
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
        var message = conversation.AddMessage(messageId, null, MessageRole.User, content);

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

        await _context.SaveChangesAsync(ct);

        message.AddDomainEvent(new MessageCreatedEvent(messageId, conversationId, tenantId, userId, content));

        var recentMessages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync(ct);

        var history = string.Join("\n", recentMessages
            .Select(m => $"{(m.Role == MessageRole.User ? "User" : "Assistant")}: {m.Content}"));

        var responseBuffer = new StringBuilder();
        var responseMessageId = Guid.NewGuid();
        var conversationGroup = $"conversation_{conversationId}";

        await foreach (var chunk in _aiService.StreamChatAsync(content, history, null, ct))
        {
            responseBuffer.Append(chunk);
            await _streaming.SendStreamChunkAsync(conversationId, chunk, ct);
        }

        var aiResponse = responseBuffer.ToString();
        var responseMessage = conversation.AddMessage(responseMessageId, messageId, MessageRole.Assistant, aiResponse);

        await _context.SaveChangesAsync(ct);

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
