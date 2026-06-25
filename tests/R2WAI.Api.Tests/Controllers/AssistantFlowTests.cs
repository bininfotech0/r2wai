using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class AssistantFlowTests : IntegrationTestBase
{
    public AssistantFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAssistants_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/assistants");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAssistant_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/assistants", new
        {
            Name = "Test",
            Type = "General"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPromptTemplates_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/assistants/prompt-templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublishAssistant_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/assistants/{Guid.NewGuid()}/publish", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChatWithAssistant_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/assistants/{Guid.NewGuid()}/chat", new
        {
            Message = "Hello"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
