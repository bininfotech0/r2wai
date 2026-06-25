using System.Net;

namespace R2WAI.Api.Tests.Controllers;

public class OperationsFlowTests : IntegrationTestBase
{
    public OperationsFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetHealth_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/operations/health");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMetrics_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/operations/metrics");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditLogs_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/operations/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExportAuditLogs_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/operations/audit-logs/export?format=csv");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetErrors_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/operations/errors");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkflowInstances_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/operations/workflow-instances");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
