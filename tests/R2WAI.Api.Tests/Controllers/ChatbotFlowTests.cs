using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class ChatbotFlowTests : IntegrationTestBase
{
    public ChatbotFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetChatbots_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/chatbots");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateChatbot_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/chatbots", new
        {
            Name = "Test Chatbot"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChatbotChat_NonexistentChatbot_ReturnsNotFound()
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/chatbots/{Guid.NewGuid()}/chat",
            new { Message = "Hello" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChatbotChat_EndpointExists()
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/chatbots/{Guid.NewGuid()}/chat",
            new { Message = "Hello" });
        Assert.NotEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task DeleteChatbot_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/chatbots/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
