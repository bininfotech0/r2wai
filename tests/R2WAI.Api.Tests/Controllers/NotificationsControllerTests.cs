using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Controllers;

/// <summary>
/// docs/api/MISSING-BACKEND-ENDPOINTS.md §3.7 #80 (unread-count half) — GetList already computed
/// this alongside a full page of results; this is the same count as its own lightweight route.
/// </summary>
public class NotificationsControllerTests : IntegrationTestBase
{
    public NotificationsControllerTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    private async Task SeedNotificationAsync(Guid tenantId, Guid userId, bool isRead)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notification = new Notification(Guid.NewGuid(), tenantId, userId, "Title", "Message");
        if (isRead) notification.MarkRead();
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetUnreadCount_RequiresAuthentication()
    {
        var response = await Client.GetAsync("/api/v1/notifications/unread-count");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Matches the seeded default tenant/admin id (ApplicationDbContextSeed) used throughout this
    // test project's fixtures, per IntegrationTestBase.SignedInScope's own convention.
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private record UnreadCountResponse(int UnreadCount);

    [Fact]
    public async Task GetUnreadCount_GoesThroughTheRealEndpoint_AndCountsOnlyTheCallersOwnUnread()
    {
        var adminClient = await GetAuthenticatedClientAsync();
        var otherUserId = Guid.NewGuid();

        var before = await adminClient.GetFromJsonAsync<UnreadCountResponse>("/api/v1/notifications/unread-count");

        // Two unread + one already-read for the caller, plus an unread one for someone else in the
        // same tenant — the real regression this endpoint could get wrong is counting every
        // tenant-wide unread notification instead of just this user's own.
        await SeedNotificationAsync(DefaultTenantId, AdminUserId, isRead: false);
        await SeedNotificationAsync(DefaultTenantId, AdminUserId, isRead: false);
        await SeedNotificationAsync(DefaultTenantId, AdminUserId, isRead: true);
        await SeedNotificationAsync(DefaultTenantId, otherUserId, isRead: false);

        var after = await adminClient.GetFromJsonAsync<UnreadCountResponse>("/api/v1/notifications/unread-count");

        Assert.Equal(before!.UnreadCount + 2, after!.UnreadCount);
    }
}
