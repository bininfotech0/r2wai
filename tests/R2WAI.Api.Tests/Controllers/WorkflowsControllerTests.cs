using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class WorkflowsControllerTests : IntegrationTestBase
{
    public WorkflowsControllerTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetWorkflows_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/v1/workflows");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateWorkflow_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/workflows", new
        {
            Name = "Test",
            Type = "sequential"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
