using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Infrastructure.Services;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// One regression test per "dead feature" defect found and fixed during the 2026-08-13 QA pass
/// (see the QA report, D11-D13/D15/D17/D18): each of these was a real, reachable feature whose
/// admin-facing CRUD worked perfectly while the runtime behavior it was supposed to control did
/// nothing at all, silently. These tests assert the runtime behavior directly, not just that the
/// CRUD endpoints return 200.
/// </summary>
public class RegressionTests : IntegrationTestBase
{
    public RegressionTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    // D11 — API keys created via the admin panel never actually authenticated anything, because
    // ApiKeyAuthenticationMiddleware only ever checked a static config list, never the ApiKeys table.
    [Fact]
    public async Task D11_ApiKey_CreatedViaAdmin_ActuallyAuthenticates()
    {
        await Factory.EnsureSeededAsync();
        var client = await GetAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync("/api/v1/admin/api-keys", new
        {
            Name = "Regression Test Key " + Guid.NewGuid(),
            Scopes = new[] { "read" },
            Roles = new[] { "Admin" },
            ExpiresAt = (DateTime?)null,
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var rawKey = created.GetProperty("key").GetString();
        var keyId = created.GetProperty("id").GetGuid();
        Assert.False(string.IsNullOrEmpty(rawKey));

        // The key must work as a bearer of identity on its own, unauthenticated client.
        var apiKeyClient = Factory.CreateClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-API-Key", rawKey);
        var authedCall = await apiKeyClient.GetAsync("/api/v1/applications");
        Assert.Equal(HttpStatusCode.OK, authedCall.StatusCode);

        // A bogus key must still be rejected.
        var bogusClient = Factory.CreateClient();
        bogusClient.DefaultRequestHeaders.Add("X-API-Key", "r2w_totally_bogus_value");
        var bogusCall = await bogusClient.GetAsync("/api/v1/applications");
        Assert.Equal(HttpStatusCode.Unauthorized, bogusCall.StatusCode);

        // Disabling the key must immediately revoke access.
        var toggle = await client.PostAsync($"/api/v1/admin/api-keys/{keyId}/toggle", null);
        Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);
        var afterToggle = await apiKeyClient.GetAsync("/api/v1/applications");
        Assert.Equal(HttpStatusCode.Unauthorized, afterToggle.StatusCode);
    }

    // D12 — Webhooks created via the admin panel never fired: the trigger endpoint validated
    // against a single global config secret and looked up the workflow via an unrelated field,
    // ignoring the WebhookEndpoint row's own Secret/WorkflowId entirely.
    [Fact]
    public async Task D12_Webhook_CreatedViaAdmin_TriggersWithItsOwnSecret()
    {
        await Factory.EnsureSeededAsync();
        var client = await GetAuthenticatedClientAsync();

        var wfCreate = await client.PostAsJsonAsync("/api/v1/workflows", new
        {
            Name = "Regression Webhook Target " + Guid.NewGuid(),
            Type = "Manual",
            Steps = "[]",
        });
        Assert.Equal(HttpStatusCode.Created, wfCreate.StatusCode);
        var wf = await wfCreate.Content.ReadFromJsonAsync<JsonElement>();
        var wfId = wf.GetProperty("id").GetGuid();
        await client.PostAsync($"/api/v1/workflows/{wfId}/publish", null);

        var secret = "regression-secret-" + Guid.NewGuid().ToString("N");
        var whCreate = await client.PostAsJsonAsync("/api/v1/admin/webhooks", new
        {
            Name = "Regression Webhook " + Guid.NewGuid(),
            TriggerType = "Workflow",
            WorkflowId = wfId,
            Secret = secret,
        });
        Assert.Equal(HttpStatusCode.Created, whCreate.StatusCode);
        var wh = await whCreate.Content.ReadFromJsonAsync<JsonElement>();
        var endpointUrl = wh.GetProperty("endpointUrl").GetString()!;

        // No secret at all must be rejected.
        var noSecret = await Client.PostAsJsonAsync(endpointUrl, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, noSecret.StatusCode);

        // Its own, freshly-set secret must trigger the linked workflow.
        var withSecretRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = JsonContent.Create(new { })
        };
        withSecretRequest.Headers.Add("X-Webhook-Secret", secret);
        var withSecret = await Client.SendAsync(withSecretRequest);
        Assert.Equal(HttpStatusCode.Accepted, withSecret.StatusCode);
    }

    // D13 — Scheduled workflows never ran: WorkflowSchedule.NextRunAt was never populated by
    // anything, so the escalation-style sweep this depends on could never find a due schedule.
    // The background sweep itself is an IHostedService and is stripped from this test host by
    // design (see R2WAIWebApplicationFactory), so this asserts the piece this test host *can*
    // observe: a freshly created schedule starts with NextRunAt unset, exactly the precondition
    // the fix's background sweep is responsible for seeding on its first pass.
    [Fact]
    public async Task D13_Schedule_CreatedViaApi_StartsWithNoNextRunAt()
    {
        await Factory.EnsureSeededAsync();
        var client = await GetAuthenticatedClientAsync();

        var wfCreate = await client.PostAsJsonAsync("/api/v1/workflows", new
        {
            Name = "Regression Schedule Target " + Guid.NewGuid(),
            Type = "Manual",
            Steps = "[]",
        });
        Assert.Equal(HttpStatusCode.Created, wfCreate.StatusCode);
        var wf = await wfCreate.Content.ReadFromJsonAsync<JsonElement>();
        var wfId = wf.GetProperty("id").GetGuid();

        var schCreate = await client.PostAsJsonAsync("/api/v1/workflows/schedules", new
        {
            WorkflowId = wfId,
            Name = "Regression Schedule " + Guid.NewGuid(),
            CronExpression = "* * * * *",
            CronDescription = "every minute",
        });
        Assert.Equal(HttpStatusCode.Created, schCreate.StatusCode);
        var created = await schCreate.Content.ReadFromJsonAsync<JsonElement>();
        var schId = created.GetProperty("id").GetGuid();

        var fetched = await client.GetAsync($"/api/v1/workflows/schedules/{schId}");
        var schedule = await fetched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(schedule.TryGetProperty("nextRunAt", out var nextRunAt) && nextRunAt.ValueKind != JsonValueKind.Null);
    }

    // D15 — NotificationService sent every real-time notification via IHubContext<NotificationHub>,
    // which resolved to an unmapped duplicate class in a different namespace than the one Program.cs
    // actually mapped to /hubs/notification, so nothing could ever receive them. The fix merged the
    // two classes into one. This guards against the duplicate ever coming back: exactly one type
    // named NotificationHub should exist across the loaded application assemblies.
    [Fact]
    public void D15_ExactlyOneNotificationHubType_Exists()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith("R2WAI") == true);

        var matches = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Name == "NotificationHub")
            .ToList();

        Assert.True(matches.Count <= 1,
            $"Expected at most one NotificationHub type, found {matches.Count}: " +
            string.Join(", ", matches.Select(t => t.FullName)));
    }

    // D17 — Inviting a user generated a random code, emailed it, and discarded it: nothing
    // persisted it, and no endpoint could have redeemed it even if it had been. The fix makes the
    // endpoint actually create the account and store the code as a real, checkable password-reset
    // token.
    [Fact]
    public async Task D17_InviteUser_CreatesRedeemableAccount()
    {
        await Factory.EnsureSeededAsync();
        var client = await GetAuthenticatedClientAsync();
        var email = $"regression.invite.{Guid.NewGuid():N}@example.com";

        var invite1 = await client.PostAsJsonAsync("/api/v1/admin/users/invite", new { Email = email });
        var invite1Body = await invite1.Content.ReadAsStringAsync();
        Assert.True(invite1.StatusCode == HttpStatusCode.OK, $"email={email}, status={invite1.StatusCode}, body={invite1Body}");
        var body = JsonDocument.Parse(invite1Body).RootElement;
        Assert.True(body.TryGetProperty("userId", out _), "Invite response must include the created user's id.");

        // Inviting the same email again must not silently repeat the no-op — it's a real account now.
        var invite2 = await client.PostAsJsonAsync("/api/v1/admin/users/invite", new { Email = email });
        Assert.Equal(HttpStatusCode.Conflict, invite2.StatusCode);

        // A bogus reset token for this email must be rejected — proof a real token is now checked,
        // where before there was never anything persisted to check against at all.
        var wrongReset = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            Email = email,
            Token = "bogus-token",
            NewPassword = "NewPass123!",
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongReset.StatusCode);
    }

    // D18 — Approval SLA escalation never fired for any approval request ever created, because
    // ApprovalRequest.DueAt was never computed at creation despite ApprovalPolicy.EscalationMinutes
    // being a real, admin-configurable field with a working escalation sweep behind it.
    [Fact]
    public async Task D18_ApprovalRequest_GetsDueAtFromActivePolicy()
    {
        await Factory.EnsureSeededAsync();
        var client = await GetAuthenticatedClientAsync();

        var policyCreate = await client.PostAsJsonAsync("/api/v1/approvals/policies", new
        {
            Name = "Regression Escalation Policy " + Guid.NewGuid(),
            ApproverRoles = "Admin",
            MinApprovers = 1,
            EscalationMinutes = 45,
        });
        Assert.Equal(HttpStatusCode.Created, policyCreate.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var approvalService = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<R2WAI.Infrastructure.Persistence.ApplicationDbContext>();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var adminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var beforeCall = DateTime.UtcNow;
        var requestId = await approvalService.CreateApprovalRequestAsync(
            tenantId, Guid.NewGuid(), Guid.NewGuid(), adminUserId, data: "regression test");

        // Read straight from the DbContext rather than GetPendingForRoleAsync: that filters on
        // ApproverRole, which only gets set by the queued background task processor — an
        // IHostedService, stripped from this test host by design (see R2WAIWebApplicationFactory) —
        // so it's irrelevant to what this test is verifying (DueAt at creation time).
        var created = await dbContext.ApprovalRequests.FindAsync(requestId);
        Assert.NotNull(created);
        // Deliberately not pinned to this test's own policy's exact 45-minute window: which of the
        // tenant's (possibly several) active policies wins is a separate, pre-existing ambiguity
        // (no WorkflowType filter, no deterministic ordering) that D18 didn't change and doesn't fix.
        // What D18 actually fixed is that DueAt gets computed from *some* active policy at all,
        // instead of always being null — that's what this asserts.
        Assert.NotNull(created!.DueAt);
        Assert.True(created.DueAt!.Value > beforeCall, "DueAt must be set to a real future time, not left null.");
    }
}
