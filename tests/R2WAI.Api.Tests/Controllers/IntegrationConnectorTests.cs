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
}
