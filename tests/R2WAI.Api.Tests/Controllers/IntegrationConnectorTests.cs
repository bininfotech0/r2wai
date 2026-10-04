using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class IntegrationConnectorTests : IntegrationTestBase
{
    public IntegrationConnectorTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetIntegrations_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/integrations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateIntegration_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/integrations", new
        {
            Name = "Test API",
            Type = "rest"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestConnection_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/integrations/{Guid.NewGuid()}/test", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteIntegration_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/integrations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Database")]
    [InlineData("Email")]
    [InlineData("Script")]
    [InlineData("Custom")]
    public async Task TestConnection_NonHttpType_ReportsHonestlyRatherThanFakeSuccess(string toolType)
    {
        // DynamicToolFunctionFactory only ever turns ToolType.Http rows into AI-callable functions —
        // this action previously returned an unconditional "Connection validated" success for every
        // other type despite performing zero actual checking. Must never regress to that.
        var client = await GetAuthenticatedClientAsync();
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var createResponse = await client.PostAsJsonAsync("/api/v1/integrations", new
        {
            Name = $"Non-Http Test {Guid.NewGuid():N}",
            Type = toolType,
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var testResponse = await client.PostAsync($"/api/v1/integrations/{id}/test", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, testResponse.StatusCode);
        var body = await testResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":false", body);
    }
}
