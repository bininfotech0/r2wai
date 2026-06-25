using Microsoft.AspNetCore.SignalR;

namespace R2WAI.Infrastructure.SignalR;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendAsync(string userId, string title, string message, string? type = null, string? link = null, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("ReceiveNotification", title, message, type ?? "info", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification to user {UserId}", userId);
        }
    }

    public async Task SendToTenantAsync(Guid tenantId, string title, string message, string? type = null, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.Group($"tenant_{tenantId}")
                .SendAsync("ReceiveNotification", title, message, type ?? "info", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification to tenant {TenantId}", tenantId);
        }
    }

    public async Task BroadcastAsync(string title, string message, string? type = null, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.All
                .SendAsync("ReceiveNotification", title, message, type ?? "info", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast notification");
        }
    }

    public async Task SendToGroupAsync(string groupName, string title, string message, string? type = null, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("ReceiveNotification", title, message, type ?? "info", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification to group {Group}", groupName);
        }
    }

    public async Task SendToAllAsync(string title, string message, string? type = null, CancellationToken ct = default)
    {
        await BroadcastAsync(title, message, type, ct);
    }
}
