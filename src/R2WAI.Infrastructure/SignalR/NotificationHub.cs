using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace R2WAI.Infrastructure.SignalR;

// This class used to be a near-duplicate of R2WAI.Api.Hubs.NotificationHub, but was never mapped
// to any route — Program.cs only ever mapped the Api.Hubs copy. Since NotificationService (right
// next to this file) sends via IHubContext<NotificationHub>, which resolved to this unmapped type
// by same-namespace lookup, every notification it sent had zero possible recipients: SignalR
// doesn't error on sending to an empty group, so this failed completely silently. Fixed by making
// this the one real implementation (merged from the Api.Hubs version, which is what clients were
// actually connecting to) and repointing Program.cs's MapHub call here instead — Api can reference
// Infrastructure, but not the other way around, so the hub type has to live on this side for both
// the mapping and the sender to agree on the same type.
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
