using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Services;
using R2WAI.Infrastructure.Services.BackgroundJobs;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// P0-5 fallout: every IBackgroundJobHandler runs exclusively from BackgroundJobProcessor, a background
/// IHostedService with no HttpContext/ambient tenant (confirmed by grep — nothing else resolves
/// IBackgroundJobHandler). Two handlers had ambient-filtered lookups with no .IgnoreQueryFilters():
///
/// NotifyApproversJobHandler's approver query ANDed the ambient filter's (now-false, no-tenant) clause
/// onto its own already-correct `u.TenantId == payload.TenantId` check, so it always returned zero
/// approvers — every approval request/escalation notification (email + in-app) silently stopped firing
/// the moment the fail-closed filter shipped, with no exception to surface it (the job just "succeeded").
///
/// IndexDocumentJobHandler → DocumentService.ProcessDocumentAsync's Documents lookup always missed too —
/// every uploaded document's indexing job threw NotFoundException, retried, and dead-lettered. Since
/// DocumentUploadedEventHandler enqueues this for every single document upload with no other path, RAG
/// indexing for newly uploaded documents was completely broken.
///
/// Both fixed the same way: NotifyApproversJobHandler has no caller besides the background processor, so
/// .IgnoreQueryFilters() was added directly (the query's own explicit TenantId check is already the real
/// scope). ProcessDocumentAsync is also called from an authenticated manual-reprocess path
/// (ProcessDocumentCommand) where the ambient filter IS the real security boundary, so it instead got an
/// optional expectedTenantId parameter (defaults to null = unchanged ambient-filtered behavior),
/// threaded from DocumentUploadedEvent.TenantId through IndexDocumentJobPayload.
/// </summary>
public class BackgroundJobHandlerTenantFilterRegressionTests : IntegrationTestBase
{
    public BackgroundJobHandlerTenantFilterRegressionTests(R2WAIWebApplicationFactory factory) : base(factory) { }

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

    private IServiceScope SignInWithNoTenantClaim()
    {
        var scope = Factory.Services.CreateScope();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return scope;
    }

    [Fact]
    public async Task NotifyApproversJobHandler_WithNoAmbientTenant_StillFindsAndNotifiesTheApprover()
    {
        var tenantId = Guid.NewGuid();
        Guid approverId;
        using (var seedScope = Factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = new Role(Guid.NewGuid(), tenantId, "Approver");
            db.Roles.Add(role);
            var approver = new User(Guid.NewGuid(), tenantId, "ext-1", "approver@example.com", "Ann", "Approver");
            db.Users.Add(approver);
            db.UserRoles.Add(new UserRole(approver.Id, role.Id));
            await db.SaveChangesAsync();
            approverId = approver.Id;
        }

        using var scope = SignInWithNoTenantClaim();
        var db2 = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = new RecordingEmailService();
        var notificationService = new RecordingNotificationService();
        var handler = new NotifyApproversJobHandler(db2, emailService, notificationService);

        var payload = new NotifyApproversJobPayload(
            ApprovalRequestId: Guid.NewGuid(), TenantId: tenantId, WorkflowId: null,
            Roles: ["Approver"], RequesterId: Guid.NewGuid(), Data: null, EscalationLevel: null);

        await handler.HandleAsync(JsonSerializer.Serialize(payload), CancellationToken.None);

        // Before the fix: the approvers query always returned empty under no ambient tenant, so
        // neither list below would ever get an entry — no exception, no visible failure.
        Assert.Contains("approver@example.com", emailService.ApprovalRequestEmailsSentTo);
        Assert.Contains(approverId.ToString(), notificationService.NotifiedUserIds);
    }

    [Fact]
    public async Task IndexDocumentJobHandler_WithNoAmbientTenant_StillFindsTheDocumentToProcess()
    {
        var tenantId = Guid.NewGuid();
        Guid documentId;
        using (var seedScope = Factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var document = new Document(Guid.NewGuid(), tenantId, Guid.NewGuid(), "report.txt",
                DocumentType.Text, "/tmp/report.txt", 10);
            db.Documents.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;
        }

        using var scope = SignInWithNoTenantClaim();
        var db2 = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
        var handler = new IndexDocumentJobHandler(documentService);

        var payload = new IndexDocumentJobPayload(documentId, tenantId);

        // Before the fix this threw NotFoundException (the Documents lookup always missed under no
        // ambient tenant) — BackgroundJobProcessor would retry then dead-letter it, silently failing
        // to index every uploaded document. Whatever happens past the lookup (real text extraction on
        // a fake path may itself fail) is not the point; reaching past NotFoundException is.
        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(JsonSerializer.Serialize(payload), CancellationToken.None));

        Assert.IsNotType<NotFoundException>(ex);
    }
}
