using System.Net;

namespace R2WAI.Api.Tests.Controllers;

public class HealthCheckTests : IntegrationTestBase
{
    public HealthCheckTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task HealthStartup_ReturnsOk()
    {
        var response = await Client.GetAsync("/health/startup");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsJsonResponse()
    {
        var response = await Client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("status", content);
    }
}
