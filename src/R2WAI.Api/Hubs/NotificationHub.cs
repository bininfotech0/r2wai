using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace R2WAI.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;

        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        if (tenantId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
        }

        _logger.LogInformation("NotificationHub: User {UserId} connected", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("NotificationHub: User {UserId} disconnected", Context.UserIdentifier);

        if (exception is not null)
        {
            _logger.LogWarning(exception, "NotificationHub: Disconnection with error");
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToUser(string userId)
    {
        if (Context.UserIdentifier != userId)
        {
            _logger.LogWarning("User {AuthUser} attempted to subscribe to notifications for {TargetUser}",
                Context.UserIdentifier, userId);
            throw new HubException("You can only subscribe to your own notifications.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        _logger.LogDebug("Connection {ConnectionId} subscribed to user {UserId}",
            Context.ConnectionId, userId);
    }

    public async Task UnsubscribeFromUser(string userId)
    {
        if (Context.UserIdentifier != userId)
            throw new HubException("You can only unsubscribe from your own notifications.");

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        _logger.LogDebug("Connection {ConnectionId} unsubscribed from user {UserId}",
            Context.ConnectionId, userId);
    }

}
