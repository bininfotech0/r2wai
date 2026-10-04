using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        ApplicationDbContext dbContext,
        ILogger<ChatHub> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
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

    private async Task<bool> ValidateConversationAccess(string conversationId, string userId)
    {
        if (!Guid.TryParse(conversationId, out var convId))
            return false;

        if (!Guid.TryParse(userId, out var uid))
            return false;

        var tenantClaim = Context.User?.FindFirst("tenant_id")?.Value;
        if (tenantClaim is null || !Guid.TryParse(tenantClaim, out var tenantId))
            return false;

        // Tenant AND owner: this used to check only the tenant, so any user in a tenant could join (and
        // post into) any other user's conversation.
        //
        // IgnoreQueryFilters: same reasoning as StatusHub.SubscribeToWorkflow — a SignalR Hub method
        // invocation doesn't reliably populate IHttpContextAccessor.HttpContext (what the ambient
        // tenant filter reads), even on an authenticated connection. The explicit `c.TenantId ==
        // tenantId` clause here, driven by Context.User, is the real check.
        return await _dbContext.Conversations.IgnoreQueryFilters()
            .AnyAsync(c => c.Id == convId && c.TenantId == tenantId && c.UserId == uid);
    }
}
