using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

/// <summary>
/// TestCasesController had zero test coverage before this — not even the unauthenticated-401 checks
/// every other controller in this project gets. Matches the established pattern here (see
/// ApprovalsControllerTests/AssistantFlowTests): every route must reject an unauthenticated caller.
/// </summary>
public class TestCasesControllerTests : IntegrationTestBase
{
    public TestCasesControllerTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetList_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/testcases");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync($"/api/v1/testcases/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/testcases", new
        {
            AssistantId = Guid.NewGuid(),
            Name = "Test",
            Question = "What is the leave policy?"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutAuth_Returns401()
    {
        var response = await Client.PutAsJsonAsync($"/api/v1/testcases/{Guid.NewGuid()}", new
        {
            Name = "Updated",
            Question = "Updated question?"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetEnabled_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/testcases/{Guid.NewGuid()}/status", new { IsEnabled = false });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Run_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/testcases/{Guid.NewGuid()}/run", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunAll_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/testcases/run-all", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/testcases/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
