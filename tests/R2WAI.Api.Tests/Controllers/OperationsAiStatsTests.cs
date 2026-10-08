using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Controllers;

/// <summary>
/// The Home/Monitor AI tiles showed "0 tokens" and "0.0s average response" next to dozens of real
/// conversations: tokens are never recorded, and SaveChanges re-stamped a user message and its reply
/// with the same save time. Unmeasured values must come back null, and real timings must survive.
/// </summary>
public class OperationsAiStatsTests : IntegrationTestBase
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public OperationsAiStatsTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SaveChanges_KeepsEachMessagesOwnCreatedAt_WhenUserTurnAndReplyAreSavedTogether()
    {
        await Factory.EnsureSeededAsync();
        using var scope = SignedInScope(TenantId, AdminUserId);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var conversation = new Conversation(Guid.NewGuid(), TenantId, AdminUserId, "ai-stats timing");
        var userMessage = conversation.AddMessage(Guid.NewGuid(), null, MessageRole.User, "hello");
        await Task.Delay(300);
        var reply = conversation.AddMessage(Guid.NewGuid(), null, MessageRole.Assistant, "hi");
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        Assert.True(reply.CreatedAt > userMessage.CreatedAt,
            $"reply {reply.CreatedAt:O} should be later than user turn {userMessage.CreatedAt:O}");
    }

    [Fact]
    public async Task GetAiStats_ReportsNullTokens_AndARealAverageResponseTime()
    {
        await Factory.EnsureSeededAsync();
        using (var scope = SignedInScope(TenantId, AdminUserId))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var conversation = new Conversation(Guid.NewGuid(), TenantId, AdminUserId, "ai-stats endpoint");
            conversation.AddMessage(Guid.NewGuid(), null, MessageRole.User, "question");
            await Task.Delay(300);
            conversation.AddMessage(Guid.NewGuid(), null, MessageRole.Assistant, "answer");
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        var client = await GetAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/v1/operations/ai-stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("totalTokens").ValueKind);
        Assert.Equal(JsonValueKind.Number, body.GetProperty("avgResponseTimeSec").ValueKind);
        Assert.True(body.GetProperty("avgResponseTimeSec").GetDouble() > 0);
    }
}
