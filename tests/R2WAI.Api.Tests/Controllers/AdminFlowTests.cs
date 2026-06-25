using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class AdminFlowTests : IntegrationTestBase
{
    public AdminFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetUsers_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetModels_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/models");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalytics_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/analytics");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsageAnalytics_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/analytics/usage");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditLogs_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSettings_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "User"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InviteUser_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/admin/users/invite", new
        {
            Email = "invite@test.com"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/admin/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestModelConnection_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/admin/models/{Guid.NewGuid()}/test", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
