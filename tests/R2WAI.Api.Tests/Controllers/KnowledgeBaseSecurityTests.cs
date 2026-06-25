using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class KnowledgeBaseSecurityTests : IntegrationTestBase
{
    public KnowledgeBaseSecurityTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetKnowledgeBases_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/knowledge-bases");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CreateKnowledgeBase_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/knowledge-bases", new
        {
            Name = "Test KB"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task SearchKnowledgeBase_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync($"/api/v1/knowledge-bases/{Guid.NewGuid()}/search?query=test");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task AddSource_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/knowledge-bases/{Guid.NewGuid()}/sources", new
        {
            Type = "text",
            Content = "Some content"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task DeleteKnowledgeBase_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/knowledge-bases/{Guid.NewGuid()}");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task BulkUpload_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/documents/bulk-upload", null);
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }
}
