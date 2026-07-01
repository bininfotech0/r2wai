using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class AuthFlowTests : IntegrationTestBase
{
    public AuthFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "wrong@test.com",
            Password = "wrong"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmptyBody_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new { });
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithoutToken_Returns401()
    {
        var response = await Client.PutAsJsonAsync("/api/v1/auth/profile", new
        {
            FirstName = "Test",
            LastName = "User"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutToken_Returns401()
    {
        var response = await Client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_AnyEmail_Returns200()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            Email = "anyone@test.com"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            Email = "admin@r2wai.io",
            Token = "INVALID",
            NewPassword = "NewPass@2026!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidTokens_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            AccessToken = "invalid.jwt.token",
            RefreshToken = "invalid"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        await Factory.EnsureSeededAsync();
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "admin@r2wai.io",
            Password = "R2wai_Admin!2026"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("token", body);
        Assert.Contains("refreshToken", body);
    }
}
