using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class KnowledgeBaseFlowTests : IntegrationTestBase
{
    public KnowledgeBaseFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetKnowledgeBases_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/knowledgebases");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateKnowledgeBase_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/knowledgebases", new
        {
            Name = "Test KB"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchKnowledgeBase_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/knowledgebases/{Guid.NewGuid()}/search", new
        {
            Query = "test"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteKnowledgeBase_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/knowledgebases/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
