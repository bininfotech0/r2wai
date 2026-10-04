using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Controllers;

/// <summary>
/// Implementation plan Phase 1 — the standalone "publish an assistant as its own REST endpoint"
/// surface. Covers the controller's own authorization gate (assistant-scoped API key, or Admin/
/// SystemAdmin JWT) and the "no published version" reject, both of which are safe to exercise at
/// the real HTTP layer without ever reaching a real AI provider (the version/scope checks run
/// before any IAIService call — confirmed by reading ChatWithPublishedAssistantCommandHandler).
/// Snapshot-vs-live config pinning itself is covered at the unit level
/// (ChatWithPublishedAssistantCommandHandlerTests), where the AI call is mocked.
/// </summary>
public class PublishedAssistantsControllerTests : IntegrationTestBase
{
    // Matches the seeded default tenant/admin id (ApplicationDbContextSeed), same convention as
    // every other test file that seeds directly rather than through the UI.
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public PublishedAssistantsControllerTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    private async Task<Guid> SeedAssistantAsync(bool withPublishedVersion)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assistant = new AssistantDefinition(Guid.NewGuid(), DefaultTenantId, "Published API Test Bot", AssistantType.General);
        assistant.UpdateDetails(assistant.Name, null, "You are a test assistant.", null, null);
        db.AssistantDefinitions.Add(assistant);

        if (withPublishedVersion)
        {
            var snapshotJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                Name = assistant.Name,
                Description = (string?)null,
                Type = AssistantType.General,
                SystemPrompt = "You are a test assistant.",
                ModelConfigurationId = (Guid?)null,
                KnowledgeBaseId = (Guid?)null,
                Tools = (string?)null,
                Settings = (string?)null,
                Tags = (string?)null,
                AvatarUrl = (string?)null,
            });
            var version = AssistantVersion.CreateSnapshot(Guid.NewGuid(), DefaultTenantId, assistant.Id, 1, snapshotJson);
            version.Publish(AdminUserId);
            db.Set<AssistantVersion>().Add(version);
        }

        await db.SaveChangesAsync();
        return assistant.Id;
    }

    private async Task<string> CreateScopedApiKeyAsync(HttpClient adminClient, params string[] scopes)
    {
        var response = await adminClient.PostAsJsonAsync("/api/v1/admin/api-keys", new { Name = "Published Assistant Test Key", Scopes = scopes });
        Assert.True(response.IsSuccessStatusCode, $"Create API key failed: {response.StatusCode}");
        var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("key").GetString()!;
    }

    private HttpClient CreateApiKeyClient(string rawKey)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", rawKey);
        return client;
    }

    [Fact]
    public async Task GetInfo_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync($"/api/v1/published-assistants/{Guid.NewGuid()}/info");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetInfo_AdminRole_ReturnsUnprocessableEntity_WhenNoPublishedVersion()
    {
        var assistantId = await SeedAssistantAsync(withPublishedVersion: false);
        var adminClient = await GetAuthenticatedClientAsync();

        var response = await adminClient.GetAsync($"/api/v1/published-assistants/{assistantId}/info");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetInfo_AdminRole_ReturnsInfo_WhenPublished_NoApiKeyScopeNeeded()
    {
        var assistantId = await SeedAssistantAsync(withPublishedVersion: true);
        var adminClient = await GetAuthenticatedClientAsync();

        var response = await adminClient.GetAsync($"/api/v1/published-assistants/{assistantId}/info");
        Assert.True(response.IsSuccessStatusCode, $"GetInfo failed: {response.StatusCode}");

        var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, body.RootElement.GetProperty("publishedVersionNumber").GetInt32());
    }

    [Fact]
    public async Task GetInfo_ApiKeyWithoutMatchingScope_ReturnsForbidden()
    {
        var assistantId = await SeedAssistantAsync(withPublishedVersion: true);
        var adminClient = await GetAuthenticatedClientAsync();

        // Scoped to a *different* assistant — proves this is a real per-resource check, not just
        // "any non-empty scope passes".
        var rawKey = await CreateScopedApiKeyAsync(adminClient, "read", $"assistant:{Guid.NewGuid()}");
        var keyClient = CreateApiKeyClient(rawKey);

        var response = await keyClient.GetAsync($"/api/v1/published-assistants/{assistantId}/info");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetInfo_ApiKeyWithMatchingScope_ReturnsInfo()
    {
        var assistantId = await SeedAssistantAsync(withPublishedVersion: true);
        var adminClient = await GetAuthenticatedClientAsync();

        var rawKey = await CreateScopedApiKeyAsync(adminClient, "read", $"assistant:{assistantId}");
        var keyClient = CreateApiKeyClient(rawKey);

        var response = await keyClient.GetAsync($"/api/v1/published-assistants/{assistantId}/info");

        Assert.True(response.IsSuccessStatusCode, $"GetInfo failed: {response.StatusCode}");
    }

    [Fact]
    public async Task Chat_ApiKeyWithoutMatchingScope_ReturnsForbidden_NeverReachesTheModel()
    {
        var assistantId = await SeedAssistantAsync(withPublishedVersion: true);
        var adminClient = await GetAuthenticatedClientAsync();

        var rawKey = await CreateScopedApiKeyAsync(adminClient, "write", $"assistant:{Guid.NewGuid()}");
        var keyClient = CreateApiKeyClient(rawKey);

        var response = await keyClient.PostAsJsonAsync($"/api/v1/published-assistants/{assistantId}/chat", new { Message = "Hello" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Chat_NoPublishedVersion_Returns400_NeverReachesTheModel()
    {
        var assistantId = await SeedAssistantAsync(withPublishedVersion: false);
        var adminClient = await GetAuthenticatedClientAsync();

        var response = await adminClient.PostAsJsonAsync($"/api/v1/published-assistants/{assistantId}/chat", new { Message = "Hello" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
