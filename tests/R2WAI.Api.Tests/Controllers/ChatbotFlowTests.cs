using System.Net;
using System.Net.Http.Headers;
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

    [Fact]
    public async Task SubmitFeedback_NonexistentChatbot_ReturnsNotFound()
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/chatbots/{Guid.NewGuid()}/feedback", new { Rating = "up" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SubmitFeedback_InvalidRating_ReturnsBadRequest()
    {
        var adminClient = await GetAuthenticatedClientAsync();
        var chatbotId = await CreateActiveChatbotAsync(adminClient, "Feedback Validation Bot");

        var response = await Client.PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/feedback", new { Rating = "sideways" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitFeedback_ActiveChatbotValidRating_ReturnsOk()
    {
        var adminClient = await GetAuthenticatedClientAsync();
        var chatbotId = await CreateActiveChatbotAsync(adminClient, "Feedback OK Bot");

        // Anonymous caller — same as the widget bundle's own postFeedback, no auth header.
        var response = await Client.PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/feedback", new { Rating = "up" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_NonexistentChatbot_ReturnsNotFound()
    {
        var content = BuildAttachment("text/plain", "hello world", "note.txt");
        var response = await Client.PostAsync($"/api/v1/chatbots/{Guid.NewGuid()}/messages/attachment", content);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_NoFile_ReturnsBadRequest()
    {
        var adminClient = await GetAuthenticatedClientAsync();
        var chatbotId = await CreateActiveChatbotAsync(adminClient, "Attachment No-File Bot");

        var response = await Client.PostAsync($"/api/v1/chatbots/{chatbotId}/messages/attachment", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_DisallowedContentType_ReturnsBadRequest()
    {
        var adminClient = await GetAuthenticatedClientAsync();
        var chatbotId = await CreateActiveChatbotAsync(adminClient, "Attachment Bad-Type Bot");

        // .docx/Office formats are deliberately excluded for this anonymous route — see
        // ChatbotsController.AllowedAttachmentContentTypes's comment.
        var content = BuildAttachment(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "fake docx bytes", "resume.docx");
        var response = await Client.PostAsync($"/api/v1/chatbots/{chatbotId}/messages/attachment", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_ActiveChatbotValidFile_ReturnsOkWithUrl()
    {
        var adminClient = await GetAuthenticatedClientAsync();
        var chatbotId = await CreateActiveChatbotAsync(adminClient, "Attachment OK Bot");

        // Anonymous caller — same as the widget bundle's own upload call, no auth header.
        var content = BuildAttachment("text/plain", "hello from a widget visitor", "note.txt");
        var response = await Client.PostAsync($"/api/v1/chatbots/{chatbotId}/messages/attachment", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("url").GetString()));
        Assert.Equal("note.txt", body.GetProperty("fileName").GetString());
    }

    private static MultipartFormDataContent BuildAttachment(string contentType, string text, string fileName)
    {
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(text));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var content = new MultipartFormDataContent { { fileContent, "file", fileName } };
        return content;
    }

    private static async Task<Guid> CreateActiveChatbotAsync(HttpClient adminClient, string name)
    {
        var create = await adminClient.PostAsJsonAsync("/api/v1/chatbots", new { Name = name });
        var chatbotId = (await create.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        await adminClient.PostAsJsonAsync($"/api/v1/chatbots/{chatbotId}/status", new { Status = "Active" });
        return chatbotId;
    }
}
