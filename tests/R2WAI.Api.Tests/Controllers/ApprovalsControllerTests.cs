using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class ApprovalsControllerTests : IntegrationTestBase
{
    public ApprovalsControllerTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetPending_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/v1/approvals/pending");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPolicies_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/v1/approvals/policies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Approve_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/approvals/{Guid.NewGuid()}/approve",
            new { Comments = "test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
