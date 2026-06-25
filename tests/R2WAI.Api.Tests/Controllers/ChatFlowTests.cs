using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class ChatFlowTests : IntegrationTestBase
{
    public ChatFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetConversations_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/chat/conversations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateConversation_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/chat/conversations", new
        {
            Title = "Test"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteConversation_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/chat/conversations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSuggestedActions_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/chat/suggested-actions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StreamChat_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/chat/stream", new
        {
            Message = "Hello"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
