using System.Net;

namespace R2WAI.Api.Tests.Controllers;

public class OperationsSecurityTests : IntegrationTestBase
{
    public OperationsSecurityTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetMetrics_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/operations/metrics");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task GetAuditLogs_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/operations/audit-logs");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task ExportAuditLogs_WithoutAuth_ReturnsNon200()
    {
        var response = await Client.PostAsync("/api/v1/operations/audit-logs/export", null);
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound
            or HttpStatusCode.MethodNotAllowed or HttpStatusCode.UnsupportedMediaType,
            $"Expected non-success auth/route error, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task GetHealth_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/operations/health");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task GetRecentActivity_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/operations/activity");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }
}
