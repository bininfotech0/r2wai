using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Assistants.Commands;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ChatHub> _logger;
    private readonly IAIService _aiService;
    private readonly IKnowledgeBaseService _knowledgeBaseService;

    public ChatHub(
        ApplicationDbContext dbContext,
        ILogger<ChatHub> logger,
        IAIService aiService,
        IKnowledgeBaseService knowledgeBaseService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _aiService = aiService;
        _knowledgeBaseService = knowledgeBaseService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;

        if (!string.IsNullOrEmpty(tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
        }

        _logger.LogInformation("ChatHub: User {UserId} connected (ConnectionId: {ConnectionId})",
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("ChatHub: User {UserId} disconnected (ConnectionId: {ConnectionId})",
            Context.UserIdentifier, Context.ConnectionId);

        if (exception is not null)
        {
            _logger.LogWarning(exception, "ChatHub: Disconnection with error");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(string conversationId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return;

        var authorized = await ValidateConversationAccess(conversationId, userId);
        if (!authorized)
        {
            _logger.LogWarning("User {UserId} attempted to join unauthorized conversation {ConversationId}",
                userId, conversationId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        _logger.LogDebug("User {UserId} joined conversation {ConversationId}", userId, conversationId);
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        _logger.LogDebug("User {UserId} left conversation {ConversationId}",
            Context.UserIdentifier, conversationId);
    }

    public async Task SendMessage(string conversationId, string message)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return;

        var authorized = await ValidateConversationAccess(conversationId, userId);
        if (!authorized)
        {
            _logger.LogWarning("User {UserId} attempted to send message to unauthorized conversation {ConversationId}",
                userId, conversationId);
            return;
        }

        _logger.LogInformation("User {UserId} sent message to conversation {ConversationId}",
            userId, conversationId);

        var messageId = Guid.NewGuid().ToString();
        await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveMessage", new
        {
            id = messageId,
            conversationId,
            content = message,
            userId,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task SendTypingIndicator(string conversationId, bool isTyping)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return;

        var authorized = await ValidateConversationAccess(conversationId, userId);
        if (!authorized)
            return;

        await Clients.OthersInGroup($"conversation_{conversationId}").SendAsync("UserTyping", new
        {
            conversationId,
            userId,
            isTyping
        });
    }

    public async Task StreamChat(Guid assistantId, string message, Guid? conversationId = null)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("StreamError", new { message = "Unauthorized" });
            return;
        }

        try
        {
            var assistant = await _dbContext.AssistantDefinitions
                .Include(a => a.KnowledgeBase)
                .FirstOrDefaultAsync(a => a.Id == assistantId);

            if (assistant is null)
            {
                await Clients.Caller.SendAsync("StreamError", new { message = "Assistant not found" });
                return;
            }

            _logger.LogInformation(
                "StreamChat started: User {UserId}, Assistant {AssistantId}, Conversation {ConversationId}",
                userId, assistantId, conversationId);

            string? context = null;
            List<CitationDto>? citations = null;

            if (assistant.KnowledgeBaseId.HasValue)
            {
                try
                {
                    var searchResult = await _knowledgeBaseService.SearchKnowledgeBaseAsync(
                        assistant.KnowledgeBaseId.Value, message, 1, 5, Context.ConnectionAborted);

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
                    _logger.LogWarning(ex, "Failed to search knowledge base {KBId} for assistant {AssistantId}",
                        assistant.KnowledgeBaseId.Value, assistant.Id);
                }
            }

            var systemPrompt = assistant.SystemPrompt ?? "You are a helpful AI assistant.";

            await foreach (var chunk in _aiService.StreamChatAsync(message, context, systemPrompt, Context.ConnectionAborted))
            {
                await Clients.Caller.SendAsync("ReceiveStreamChunk", new
                {
                    assistantId,
                    conversationId,
                    content = chunk,
                    timestamp = DateTime.UtcNow
                });
            }

            if (citations is { Count: > 0 })
            {
                await Clients.Caller.SendAsync("ReceiveCitations", new
                {
                    assistantId,
                    conversationId,
                    citations
                });
            }

            await Clients.Caller.SendAsync("StreamComplete", new
            {
                assistantId,
                conversationId,
                timestamp = DateTime.UtcNow
            });

            _logger.LogInformation(
                "StreamChat completed: User {UserId}, Assistant {AssistantId}, Citations {CitationCount}",
                userId, assistantId, citations?.Count ?? 0);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("StreamChat cancelled for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StreamChat failed for user {UserId}, assistant {AssistantId}",
                userId, assistantId);
            await Clients.Caller.SendAsync("StreamError", new { message = "An error occurred during streaming" });
        }
    }

    private async Task<bool> ValidateConversationAccess(string conversationId, string userId)
    {
        if (!Guid.TryParse(conversationId, out var convId))
            return false;

        if (!Guid.TryParse(userId, out var uid))
            return false;

        var tenantClaim = Context.User?.FindFirst("tenant_id")?.Value;
        if (tenantClaim is null || !Guid.TryParse(tenantClaim, out var tenantId))
            return false;

        return await _dbContext.Conversations
            .AnyAsync(c => c.Id == convId && c.TenantId == tenantId);
    }
}
