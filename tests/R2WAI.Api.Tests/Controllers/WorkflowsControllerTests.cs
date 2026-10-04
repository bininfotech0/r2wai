using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class WorkflowsControllerTests : IntegrationTestBase
{
    public WorkflowsControllerTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetWorkflows_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/v1/workflows");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateWorkflow_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/workflows", new
        {
            Name = "Test",
            Type = "sequential"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTemplates_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/v1/workflows/templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTemplates_ReturnsTheFiveStaticDefaults()
    {
        var client = await GetAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/workflows/templates");
        Assert.True(response.IsSuccessStatusCode, $"GET templates failed: {response.StatusCode}");

        var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(5, items.Count);
        Assert.Contains(items, i => i.GetProperty("id").GetString() == "invoice-approval");
    }

    // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.7 #82 — this was the actual gap: the 5 templates
    // were hardcoded with no way to edit them at all.
    [Fact]
    public async Task SetTemplate_ThenGet_ReflectsTheOverride_OtherTemplatesUnaffected()
    {
        var client = await GetAuthenticatedClientAsync();

        var putResponse = await client.PutAsJsonAsync("/api/v1/workflows/templates/invoice-approval", new
        {
            Name = "Regression Invoice Flow",
            Description = "Edited by a test",
            Type = "Approval",
            Steps = new[] { new { Name = "Only Step", Action = "Action", AssignedRole = "Requester", Order = 0 } },
        });
        Assert.True(putResponse.IsSuccessStatusCode, $"PUT failed: {putResponse.StatusCode}");

        var getResponse = await client.GetAsync("/api/v1/workflows/templates");
        var body = System.Text.Json.JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();

        var edited = items.Single(i => i.GetProperty("id").GetString() == "invoice-approval");
        Assert.Equal("Regression Invoice Flow", edited.GetProperty("name").GetString());
        Assert.Single(edited.GetProperty("steps").EnumerateArray());

        var untouched = items.Single(i => i.GetProperty("id").GetString() == "purchase-request");
        Assert.Equal("Purchase Request", untouched.GetProperty("name").GetString());
    }

    [Fact]
    public async Task SetTemplate_UnknownTemplateId_Returns404()
    {
        var client = await GetAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/workflows/templates/not-a-real-template", new
        {
            Name = "x",
            Type = "Approval",
            Steps = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetTemplate_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/api/v1/workflows/templates/invoice-approval", new
        {
            Name = "x",
            Type = "Approval",
            Steps = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
