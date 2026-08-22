using System.Net;

namespace R2WAI.Api.Tests.Controllers;

/// <summary>
/// TestRunsController had zero test coverage before this. Matches the established pattern here (see
/// ApprovalsControllerTests/AssistantFlowTests): every route must reject an unauthenticated caller.
/// </summary>
public class TestRunsControllerTests : IntegrationTestBase
{
    public TestRunsControllerTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetList_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/testruns");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync($"/api/v1/testruns/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
