using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class IntegrationFlowTests : IntegrationTestBase
{
    public IntegrationFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

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
            Name = "Test",
            Type = "HTTP"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ToggleIntegration_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/integrations/{Guid.NewGuid()}/toggle", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestIntegration_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/integrations/{Guid.NewGuid()}/test", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
