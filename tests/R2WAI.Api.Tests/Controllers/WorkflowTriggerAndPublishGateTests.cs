using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace R2WAI.Api.Tests.Controllers;

/// <summary>
/// 2026-10-16 feature-honesty fixes: a workflow can only be saved with a trigger something actually
/// fires (Manual / Webhook / a valid schedule), and can only be published once no step is the no-op
/// "Action" placeholder — a published automation runs unattended, so nothing in it may silently do
/// nothing.
/// </summary>
public class WorkflowTriggerAndPublishGateTests(R2WAIWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string RealSteps = """[{"order":0,"name":"Notify HR","type":"Email"}]""";

    [Theory]
    [InlineData("Payment Received")]
    [InlineData("Form Submitted")]
    [InlineData("Schedule")]
    [InlineData("schedule:every morning|UTC")]
    public async Task Create_WithATriggerThatNeverFires_IsRejected(string trigger)
    {
        await Factory.EnsureSeededAsync();
        var client = await GetAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/workflows", new { Name = "Trigger " + Guid.NewGuid(), Trigger = trigger, Steps = RealSteps });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Webhook")]
    [InlineData("schedule:0 9 * * 1-5|Asia/Kolkata")]
    public async Task Create_WithARunnableTrigger_Succeeds(string? trigger)
    {
        await Factory.EnsureSeededAsync();
        var client = await GetAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/workflows", new { Name = "Trigger " + Guid.NewGuid(), Trigger = trigger, Steps = RealSteps });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Publish_WithAPlaceholderStep_Returns422NamingTheStep()
    {
        await Factory.EnsureSeededAsync();
        var client = await GetAuthenticatedClientAsync();
        var id = await CreateAsync(client, """[{"order":0,"name":"Submit Invoice","type":"Action"},{"order":1,"name":"Notify","type":"Email"}]""");

        var response = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Submit Invoice", body.GetProperty("errors").GetProperty("Steps")[0].GetString());

        var workflow = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workflows/{id}");
        Assert.NotEqual("Published", workflow.GetProperty("versionStatus").GetString());
    }

    [Fact]
    public async Task Publish_WithOnlyRealSteps_Succeeds()
    {
        await Factory.EnsureSeededAsync();
        var client = await GetAuthenticatedClientAsync();
        var id = await CreateAsync(client, RealSteps);

        var response = await client.PostAsync($"/api/v1/workflows/{id}/publish", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string steps)
    {
        var response = await client.PostAsJsonAsync("/api/v1/workflows", new { Name = "Gate " + Guid.NewGuid(), Steps = steps });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
