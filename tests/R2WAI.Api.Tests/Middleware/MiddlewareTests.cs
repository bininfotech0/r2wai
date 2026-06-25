using System.Net;

namespace R2WAI.Api.Tests.Middleware;

public class MiddlewareTests : IntegrationTestBase
{
    public MiddlewareTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SecurityHeaders_XContentTypeOptions_IsPresent()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
    }

    [Fact]
    public async Task SecurityHeaders_FrameAncestors_InCSP()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        var csp = response.Headers.GetValues("Content-Security-Policy").First();
        Assert.Contains("frame-ancestors", csp);
    }

    [Fact]
    public async Task SecurityHeaders_ReferrerPolicy_IsPresent()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        Assert.True(response.Headers.Contains("Referrer-Policy"));
    }

    [Fact]
    public async Task RateLimitHeaders_ArePresent()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        Assert.True(response.Headers.Contains("X-RateLimit-Limit"));
        Assert.True(response.Headers.Contains("X-RateLimit-Remaining"));
        Assert.True(response.Headers.Contains("X-RateLimit-Reset"));
    }

    [Fact]
    public async Task NonExistentRoute_Returns404()
    {
        var response = await Client.GetAsync("/api/v1/nonexistent-endpoint");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CSP_ContainsWss()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        if (response.Headers.Contains("Content-Security-Policy"))
        {
            var csp = response.Headers.GetValues("Content-Security-Policy").First();
            Assert.Contains("wss:", csp);
        }
    }

    [Fact]
    public async Task WebhookEndpoint_IsAccessibleAnonymously()
    {
        var response = await Client.PostAsync(
            "/api/v1/workflows/webhook/test-slug",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
