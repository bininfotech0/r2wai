using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsNonSuccess()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "invalid@test.com",
            Password = "wrong"
        });

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Profile_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
