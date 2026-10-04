using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Security;

/// <summary>
/// P0-5 fallout, found while auditing ChatbotsController's downstream calls (the "exhaustive
/// downstream-parameter-usage check" item left open after the fail-closed tenant-filter flip):
/// [AllowAnonymous] callers have no tenant_id claim, so CurrentUserService.TenantId is null. Every
/// dbContext.Chatbots lookup in ChatbotsController (public-info/chat/chat/stream/webhook) queried
/// through the ambient-filtered DbSet with no .IgnoreQueryFilters() escape hatch — under the new
/// fail-closed filter that means an anonymous caller can never find ANY chatbot, breaking the entire
/// embeddable public widget. The same gap existed one hop further in: ChatbotsController's RAG lookup
/// calls KnowledgeBaseService.SearchKnowledgeBaseAsync, which resolves the KnowledgeBase by id through
/// the same ambient filter — silently degrading a KB-linked public chatbot to ungrounded replies
/// (masked by a try/catch, so no crash, just quietly wrong answers) rather than 404ing outright.
/// Same root cause, same fix shape already proven for AuthController.Refresh/StatusHub/ChatHub:
/// explicit tenant verification instead of the ambient claim.
/// </summary>
public class ChatbotAnonymousTenantFilterRegressionTests : IntegrationTestBase
{
    public ChatbotAnonymousTenantFilterRegressionTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task PublicInfo_ForAnActiveChatbot_IsReachableByAnAnonymousCaller()
    {
        var adminClient = await GetAuthenticatedClientAsync();

        var createResponse = await adminClient.PostAsJsonAsync("/api/v1/chatbots", new { Name = "Widget Regression Bot" });
        Assert.True(createResponse.IsSuccessStatusCode, $"Create chatbot failed: {createResponse.StatusCode}");
        var chatbotId = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var statusResponse = await adminClient.PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/status", new { Status = "Active" });
        Assert.True(statusResponse.IsSuccessStatusCode, $"Activate chatbot failed: {statusResponse.StatusCode}");

        // Client (from IntegrationTestBase) carries no Authorization header — this is the anonymous
        // widget's own call, the exact scenario that 404'd before the .IgnoreQueryFilters() fix: the
        // chatbot genuinely exists and is Active, but was structurally unfindable with no ambient tenant.
        var publicInfoResponse = await Client.GetAsync($"/api/v1/chatbots/{chatbotId}/public-info");

        Assert.Equal(HttpStatusCode.OK, publicInfoResponse.StatusCode);
        var body = JsonDocument.Parse(await publicInfoResponse.Content.ReadAsStringAsync());
        Assert.Equal("Widget Regression Bot", body.RootElement.GetProperty("name").GetString());
    }

    private IServiceScope SignInWithNoTenantClaim()
    {
        var scope = Factory.Services.CreateScope();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return scope;
    }

    private async Task<Guid> SeedKnowledgeBaseAsync(Guid tenantId)
    {
        using var seedScope = Factory.Services.CreateScope();
        var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var kb = new KnowledgeBase(Guid.NewGuid(), tenantId, Guid.NewGuid(), "Anon Widget KB");
        db.KnowledgeBases.Add(kb);
        await db.SaveChangesAsync();
        return kb.Id;
    }

    [Fact]
    public async Task SearchKnowledgeBase_WithNoAmbientTenant_ButCorrectExpectedTenantId_StillFindsIt()
    {
        var tenantId = Guid.NewGuid();
        var kbId = await SeedKnowledgeBaseAsync(tenantId);

        using var scope = SignInWithNoTenantClaim();
        var service = scope.ServiceProvider.GetRequiredService<IKnowledgeBaseService>();

        // Would have thrown NotFoundException before the expectedTenantId parameter existed — an
        // anonymous scope has no ambient tenant, so the plain ambient-filtered lookup always misses.
        var result = await service.SearchKnowledgeBaseAsync(kbId, "anything", 1, 5, expectedTenantId: tenantId);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SearchKnowledgeBase_WithNoAmbientTenant_AndNoExpectedTenantId_StillThrowsNotFound()
    {
        // No regression for the many authenticated callers of this method that don't pass
        // expectedTenantId at all — omitting it must still behave exactly like before this fix.
        var tenantId = Guid.NewGuid();
        var kbId = await SeedKnowledgeBaseAsync(tenantId);

        using var scope = SignInWithNoTenantClaim();
        var service = scope.ServiceProvider.GetRequiredService<IKnowledgeBaseService>();

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.SearchKnowledgeBaseAsync(kbId, "anything", 1, 5));
    }

    [Fact]
    public async Task SearchKnowledgeBase_WithNoAmbientTenant_AndWrongExpectedTenantId_StillThrowsNotFound()
    {
        // The actual safety property: expectedTenantId is a caller-verified substitute for the ambient
        // claim, not a bypass — a mismatched tenant must not leak another tenant's knowledge base.
        var realTenantId = Guid.NewGuid();
        var someoneElsesTenantId = Guid.NewGuid();
        var kbId = await SeedKnowledgeBaseAsync(realTenantId);

        using var scope = SignInWithNoTenantClaim();
        var service = scope.ServiceProvider.GetRequiredService<IKnowledgeBaseService>();

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.SearchKnowledgeBaseAsync(kbId, "anything", 1, 5, expectedTenantId: someoneElsesTenantId));
    }
}
