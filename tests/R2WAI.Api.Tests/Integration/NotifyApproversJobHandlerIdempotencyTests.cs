using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Services.BackgroundJobs;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// Implementation plan Phase 5's crash-duplication fix, found by tracing this handler's real code
/// rather than the plan's original (wrong, for this codebase) assumption that the risk lived in
/// workflow API-call/email steps: NotifyApproversJobHandler emailed/notified every approver in a
/// loop with no per-recipient tracking, so BackgroundJobProcessor retrying the whole job (on a
/// thrown exception, or now also via its own lease-reclaim after a crash — same phase) re-ran the
/// entire loop from the top, re-notifying everyone already notified. ApprovalNotificationDispatch
/// is the per-approver marker that fixes this.
/// </summary>
public class NotifyApproversJobHandlerIdempotencyTests : IntegrationTestBase
{
    public NotifyApproversJobHandlerIdempotencyTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    private sealed class RecordingEmailService : IEmailService
    {
        public readonly List<string> ApprovalRequestEmailsSentTo = [];
        public Task SendApprovalRequestAsync(string toEmail, string toName, string workflowName, string requesterName, string? details, Guid approvalId, CancellationToken ct = default)
        {
            ApprovalRequestEmailsSentTo.Add(toEmail);
            return Task.CompletedTask;
        }
        public Task SendApprovalDecisionAsync(string toEmail, string toName, string workflowName, bool approved, string? comments, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendEscalationAsync(string toEmail, string toName, string workflowName, int escalationLevel, Guid approvalId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPasswordResetAsync(string toEmail, string toName, string resetToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendUserInviteAsync(string toEmail, string inviterName, string tenantName, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public readonly List<string> NotifiedUserIds = [];
        public Task SendAsync(string userId, string title, string message, string? type = null, string? link = null, CancellationToken ct = default)
        {
            NotifiedUserIds.Add(userId);
            return Task.CompletedTask;
        }
        public Task SendToTenantAsync(Guid tenantId, string title, string message, string? type = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task BroadcastAsync(string title, string message, string? type = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private async Task<(Guid TenantId, Guid Approver1Id, Guid Approver2Id, Guid ApprovalRequestId)> SeedTwoApproversAsync()
    {
        var tenantId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var role = new Role(Guid.NewGuid(), tenantId, "Approver");
        db.Roles.Add(role);
        var approver1 = new User(Guid.NewGuid(), tenantId, "ext-1", "approver1@example.com", "One", "Approver");
        var approver2 = new User(Guid.NewGuid(), tenantId, "ext-2", "approver2@example.com", "Two", "Approver");
        db.Users.Add(approver1);
        db.Users.Add(approver2);
        db.UserRoles.Add(new UserRole(approver1.Id, role.Id));
        db.UserRoles.Add(new UserRole(approver2.Id, role.Id));
        await db.SaveChangesAsync();

        return (tenantId, approver1.Id, approver2.Id, Guid.NewGuid());
    }

    [Fact]
    public async Task HandleAsync_RunTwiceForTheSamePayload_NotifiesEachApproverOnlyOnce()
    {
        var (tenantId, approver1Id, approver2Id, approvalRequestId) = await SeedTwoApproversAsync();
        var payload = new NotifyApproversJobPayload(
            ApprovalRequestId: approvalRequestId, TenantId: tenantId, WorkflowId: null,
            Roles: ["Approver"], RequesterId: Guid.NewGuid(), Data: null, EscalationLevel: null);
        var payloadJson = JsonSerializer.Serialize(payload);

        // First run: a normal, uninterrupted execution.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = new RecordingEmailService();
            var notificationService = new RecordingNotificationService();
            var handler = new NotifyApproversJobHandler(db, emailService, notificationService);

            await handler.HandleAsync(payloadJson, CancellationToken.None);

            Assert.Equal(2, emailService.ApprovalRequestEmailsSentTo.Count);
            Assert.Equal(2, notificationService.NotifiedUserIds.Count);
        }

        // Second run: BackgroundJobProcessor retrying the same job (thrown exception, or a
        // lease-reclaim after a crash) re-invokes HandleAsync with the identical payload — the
        // real scenario this fix closes. Neither approver should be notified again.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = new RecordingEmailService();
            var notificationService = new RecordingNotificationService();
            var handler = new NotifyApproversJobHandler(db, emailService, notificationService);

            await handler.HandleAsync(payloadJson, CancellationToken.None);

            Assert.Empty(emailService.ApprovalRequestEmailsSentTo);
            Assert.Empty(notificationService.NotifiedUserIds);
        }
    }

    [Fact]
    public async Task HandleAsync_OneApproverAlreadyDispatched_NotifiesOnlyTheRemainingApprover()
    {
        // Stand-in for "the job crashed after approver #1's send but before the loop reached
        // approver #2" — the exact partial-completion window this fix protects.
        var (tenantId, approver1Id, approver2Id, approvalRequestId) = await SeedTwoApproversAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<ApprovalNotificationDispatch>().Add(
                new ApprovalNotificationDispatch(Guid.NewGuid(), tenantId, approvalRequestId, escalationLevel: 0, approver1Id));
            await db.SaveChangesAsync();
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = new RecordingEmailService();
            var notificationService = new RecordingNotificationService();
            var handler = new NotifyApproversJobHandler(db, emailService, notificationService);

            var payload = new NotifyApproversJobPayload(
                ApprovalRequestId: approvalRequestId, TenantId: tenantId, WorkflowId: null,
                Roles: ["Approver"], RequesterId: Guid.NewGuid(), Data: null, EscalationLevel: null);
            await handler.HandleAsync(JsonSerializer.Serialize(payload), CancellationToken.None);

            Assert.Equal(["approver2@example.com"], emailService.ApprovalRequestEmailsSentTo);
            Assert.Equal([approver2Id.ToString()], notificationService.NotifiedUserIds);
        }
    }

    [Fact]
    public async Task HandleAsync_EscalationLevelIsPartOfTheDedupKey_NotTreatedAsAlreadyNotified()
    {
        // A dispatch recorded for the initial request (level 0) must not suppress a later
        // escalation (level 1) to the same approver — they're genuinely different notifications.
        var (tenantId, approver1Id, _, approvalRequestId) = await SeedTwoApproversAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<ApprovalNotificationDispatch>().Add(
                new ApprovalNotificationDispatch(Guid.NewGuid(), tenantId, approvalRequestId, escalationLevel: 0, approver1Id));
            db.Set<ApprovalNotificationDispatch>().Add(
                new ApprovalNotificationDispatch(Guid.NewGuid(), tenantId, approvalRequestId, escalationLevel: 0, Guid.NewGuid()));
            await db.SaveChangesAsync();
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = new RecordingEmailService();
            var notificationService = new RecordingNotificationService();
            var handler = new NotifyApproversJobHandler(db, emailService, notificationService);

            var escalationPayload = new NotifyApproversJobPayload(
                ApprovalRequestId: approvalRequestId, TenantId: tenantId, WorkflowId: null,
                Roles: ["Approver"], RequesterId: null, Data: null, EscalationLevel: 1);
            await handler.HandleAsync(JsonSerializer.Serialize(escalationPayload), CancellationToken.None);

            Assert.Contains("approver1@example.com", emailService.ApprovalRequestEmailsSentTo);
        }
    }
}
