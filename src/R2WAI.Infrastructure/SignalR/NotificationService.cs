using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Infrastructure.SignalR;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SendAsync(string userId, string title, string message, string? type = null, string? link = null, CancellationToken ct = default)
    {
        Notification? notification = null;

        if (Guid.TryParse(userId, out var userGuid))
        {
            try
            {
                // A brand-new scope/DbContext, deliberately not the ambient one: SendAsync is
                // routinely called from inside a domain-event handler (e.g.
                // DocumentProcessedEventHandler), which itself runs from within the triggering
                // ApplicationDbContext's own SaveChangesAsync -> DispatchDomainEventsAsync step,
                // BEFORE that entity's domain events are cleared. Reusing that same DbContext here
                // and calling SaveChangesAsync on it would recursively re-scan the still-dirty
                // ChangeTracker, re-find the same not-yet-cleared event, and re-publish it — an
                // unbounded self-amplifying loop (confirmed live: one document upload produced
                // 18,000+ Notification rows and pegged a CPU core solid). An isolated DbContext has
                // an empty ChangeTracker, so it can never re-trigger anything.
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // IgnoreQueryFilters: this can run from background/event-handler contexts with no
                // HttpContext, where the ambient tenant query filter resolves to null and would
                // otherwise silently drop the lookup — mirrors ApprovalService.EscalateOverdueAsync's
                // established pattern for tenant-scoped reads outside a request.
                var recipient = await dbContext.Users.IgnoreQueryFilters()
                    .Where(u => !u.IsDeleted)
                    .FirstOrDefaultAsync(u => u.Id == userGuid, ct);

                if (recipient is not null)
                {
                    notification = new Notification(Guid.NewGuid(), recipient.TenantId, userGuid, title, message, type, link);
                    dbContext.Notifications.Add(notification);
                    await dbContext.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist notification for user {UserId}", userId);
            }
        }

        try
        {
            await _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("ReceiveNotification", notification?.Id.ToString(), title, message, type ?? "info",
                    (notification?.CreatedAt ?? DateTime.UtcNow).ToString("O"), ct);
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
