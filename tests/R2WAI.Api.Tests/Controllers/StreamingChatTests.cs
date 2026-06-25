using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class StreamingChatTests : IntegrationTestBase
{
    public StreamingChatTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task StreamChat_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/assistants/{Guid.NewGuid()}/chat/stream", new
        {
            Message = "Hello"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Chat_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/assistants/{Guid.NewGuid()}/chat", new
        {
            Message = "Hello"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StreamChat_WithEmptyMessage_Returns400OrUnauthorized()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/assistants/{Guid.NewGuid()}/chat/stream", new
        {
            Message = ""
        });
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Chat_WithInvalidAssistantId_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/assistants/{Guid.NewGuid()}/chat", new
        {
            Message = "Hello"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
