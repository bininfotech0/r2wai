using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class WorkflowExecutionTests : IntegrationTestBase
{
    public WorkflowExecutionTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ExecuteWorkflow_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/workflows/{Guid.NewGuid()}/execute", new
        {
            Context = "test"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkflowSteps_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync($"/api/v1/workflows/instances/{Guid.NewGuid()}/steps");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RetryWorkflowStep_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/workflows/instances/{Guid.NewGuid()}/retry", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkflowInstance_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync($"/api/v1/workflows/instances/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkflowTemplates_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/workflows/templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
