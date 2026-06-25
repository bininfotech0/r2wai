namespace R2WAI.Application.Common.Interfaces;

public interface INotificationService
{
    Task SendAsync(string userId, string title, string message, string? type = null, string? link = null, CancellationToken ct = default);
    Task SendToTenantAsync(Guid tenantId, string title, string message, string? type = null, CancellationToken ct = default);
    Task BroadcastAsync(string title, string message, string? type = null, CancellationToken ct = default);
}
