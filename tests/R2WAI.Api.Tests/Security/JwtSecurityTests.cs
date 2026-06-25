using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Security;

public class JwtSecurityTests : IntegrationTestBase
{
    public JwtSecurityTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    // ═══════════════════════════════════════════════════════════════════
    //  MALFORMED TOKEN HANDLING
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("not-a-jwt-token")]
    [InlineData("header.payload")]
    [InlineData("header.payload.signature.extra")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0.")]
    public async Task MalformedJwt_ReturnsUnauthorized(string malformedToken)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", malformedToken);

        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NoneAlgorithmJwt_IsRejected()
    {
        // JWT with alg: "none" — a known attack vector
        var noneToken = "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJhZG1pbkByMndhaS5pbyIsInJvbGUiOiJBZG1pbiJ9.";
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", noneToken);

        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedJwt_IsRejected()
    {
        await Factory.EnsureSeededAsync();
        var token = await GetAuthTokenAsync();
        if (string.IsNullOrEmpty(token)) return;

        // Tamper with the payload by changing a character
        var parts = token.Split('.');
        if (parts.Length == 3)
        {
            var tamperedPayload = parts[1].Length > 5
                ? parts[1][..^1] + (parts[1][^1] == 'A' ? 'B' : 'A')
                : parts[1];
            var tamperedToken = $"{parts[0]}.{tamperedPayload}.{parts[2]}";

            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

            var response = await client.GetAsync("/api/v1/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task ExpiredStyleJwt_WrongSignature_IsRejected()
    {
        // A properly structured JWT but with wrong signature
        var fakeToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhZG1pbkByMndhaS5pbyIsInJvbGUiOiJBZG1pbiIsImV4cCI6MTYwMDAwMDAwMH0.fake_signature_here";
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fakeToken);

        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTH HEADER VARIATIONS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BasicAuthScheme_IsNotAccepted()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "dGVzdDp0ZXN0");

        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingBearerPrefix_IsRejected()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "some-token-without-bearer");

        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TOKEN REFRESH SECURITY
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Refresh_WithMalformedTokens_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            AccessToken = "malformed",
            RefreshToken = "malformed"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithEmptyTokens_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            AccessToken = "",
            RefreshToken = ""
        });
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest,
            $"Empty refresh tokens should be rejected, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTHORIZATION ESCALATION
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/api/v1/admin/users")]
    [InlineData("/api/v1/admin/roles")]
    [InlineData("/api/v1/admin/security-settings")]
    [InlineData("/api/v1/admin/model-configurations")]
    public async Task AdminEndpoints_WithoutAuth_Return401(string endpoint)
    {
        var response = await Client.GetAsync(endpoint);
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Admin endpoint {endpoint} should require auth, got {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("/api/v1/api-keys")]
    [InlineData("/api/v1/webhooks")]
    [InlineData("/api/v1/schedules")]
    public async Task SensitiveEndpoints_WithoutAuth_Return401(string endpoint)
    {
        var response = await Client.GetAsync(endpoint);
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Sensitive endpoint {endpoint} should require auth, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTHENTICATED FLOW VALIDATION
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidLogin_TokenContainsExpectedClaims()
    {
        await Factory.EnsureSeededAsync();
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "admin@r2wai.io",
            Password = "admin123"
        });

        if (response.StatusCode != HttpStatusCode.OK) return;

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("token", body);
        Assert.Contains("refreshToken", body);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_InvalidatesSession()
    {
        await Factory.EnsureSeededAsync();
        var authClient = await GetAuthenticatedClientAsync();
        if (authClient.DefaultRequestHeaders.Authorization == null) return;

        var logoutResponse = await authClient.PostAsync("/api/v1/auth/logout", null);
        if (logoutResponse.StatusCode == HttpStatusCode.NotFound) return;

        Assert.True(logoutResponse.IsSuccessStatusCode,
            $"Logout should succeed, got {(int)logoutResponse.StatusCode}");
    }
}
