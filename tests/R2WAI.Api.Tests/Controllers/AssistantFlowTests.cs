using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace R2WAI.Api.Tests.Controllers;

public class AssistantFlowTests : IntegrationTestBase
{
    public AssistantFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAssistants_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/assistants");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAssistant_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/assistants", new
        {
            Name = "Test",
            Type = "General"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPromptTemplates_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/assistants/prompt-templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetPromptTemplate_ThenGet_ReflectsTheOverride_ThenReset_FallsBackToDefault()
    {
        var client = await GetAuthenticatedClientAsync();

        var setResponse = await client.PutAsJsonAsync("/api/v1/assistants/prompt-templates/HR", new { Content = "Regression-test HR wording" });
        Assert.True(setResponse.IsSuccessStatusCode, $"PUT failed: {setResponse.StatusCode}");

        var getAfterSet = await client.GetAsync("/api/v1/assistants/prompt-templates");
        var bodyAfterSet = JsonDocument.Parse(await getAfterSet.Content.ReadAsStringAsync());
        var hrEntryAfterSet = bodyAfterSet.RootElement.GetProperty("items").EnumerateArray()
            .First(e => e.GetProperty("type").GetString() == "HR");
        Assert.Equal("Regression-test HR wording", hrEntryAfterSet.GetProperty("prompt").GetString());

        var resetResponse = await client.DeleteAsync("/api/v1/assistants/prompt-templates/HR");
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        var getAfterReset = await client.GetAsync("/api/v1/assistants/prompt-templates");
        var bodyAfterReset = JsonDocument.Parse(await getAfterReset.Content.ReadAsStringAsync());
        var hrEntryAfterReset = bodyAfterReset.RootElement.GetProperty("items").EnumerateArray()
            .First(e => e.GetProperty("type").GetString() == "HR");
        Assert.NotEqual("Regression-test HR wording", hrEntryAfterReset.GetProperty("prompt").GetString());
    }

    [Fact]
    public async Task SetPromptTemplate_UnknownType_Returns400()
    {
        var client = await GetAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/assistants/prompt-templates/NotARealType", new { Content = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetPromptTemplate_WithoutAuth_Returns401()
    {
        var response = await Client.PutAsJsonAsync("/api/v1/assistants/prompt-templates/HR", new { Content = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublishAssistant_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/assistants/{Guid.NewGuid()}/publish", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChatWithAssistant_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/assistants/{Guid.NewGuid()}/chat", new
        {
            Message = "Hello"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAssistant_StartsWithNoToolsEnabled_NotEveryTool()
    {
        // P0-4 (2026-09-20 audit): a brand-new assistant used to silently get every tool in the tenant
        // (GetEnabledToolIds' null-means-all fallback, meant only to keep pre-existing assistants
        // working unchanged) — now it starts deny-by-default, an admin opts it into tools explicitly.
        var client = await GetAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/assistants", new
        {
            Name = "Deny-By-Default Regression Assistant",
            Type = "General"
        });

        Assert.True(response.IsSuccessStatusCode, $"Create assistant failed: {response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var tools = body.GetProperty("tools").GetString();

        Assert.Equal("[]", tools);
    }
}
