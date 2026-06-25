using System.Net;

namespace R2WAI.Api.Tests.Controllers;

public class DocumentsControllerTests : IntegrationTestBase
{
    public DocumentsControllerTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetDocuments_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/v1/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutAuth_ReturnsUnauthorized()
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("test"), "file", "test.txt");
        var response = await Client.PostAsync("/api/v1/documents/upload", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
