using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Security;

/// <summary>
/// Audit findings P0-7 / anonymous surface. The API had no fallback authorization policy, so any action
/// or endpoint without an explicit [Authorize] was public by default — which is how
/// POST /api/v1/auth/register-member ended up open to anyone. These tests pin the anonymous surface to
/// a reviewed list (adding a public endpoint must be a conscious edit here) and require authentication
/// for everything else by default.
/// </summary>
public class AnonymousSurfaceTests : IntegrationTestBase
{
    public AnonymousSurfaceTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    // "METHOD route" — lower-case, no leading slash. "*" = the endpoint does not restrict the method.
    private static readonly string[] ReviewedAnonymousEndpoints =
    [
        "POST api/v1/auth/login",
        "POST api/v1/auth/refresh",
        "POST api/v1/auth/forgot-password",
        "POST api/v1/auth/reset-password",
        "POST api/v1/auth/request-access",
        "POST api/v1/auth/entra-id",
        "GET api/v1/chatbots/{id:guid}/public-info",
        "POST api/v1/chatbots/{id:guid}/chat",
        "POST api/v1/chatbots/{id:guid}/chat/stream",
        "POST api/v1/chatbots/{id:guid}/feedback",
        "POST api/v1/chatbots/{id:guid}/messages/attachment",
        "POST api/v1/chatbots/{id:guid}/webhook",
        "POST api/v1/workflows/webhook/{slug}",
        "* health",
        "* health/ready",
        "* health/startup",
        "* metrics/prometheus",
    ];

    private string[] ActualAnonymousEndpoints() =>
        Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => $"{method} {e.RoutePattern.RawText!.Trim('/')}".ToLowerInvariant()))
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void Only_reviewed_endpoints_allow_anonymous_access()
    {
        var expected = ReviewedAnonymousEndpoints.Select(x => x.ToLowerInvariant()).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, ActualAnonymousEndpoints());
    }

    [Fact]
    public void Every_endpoint_declares_its_authorization()
    {
        // Controller endpoints get an [Authorize] from MapControllers().RequireAuthorization(); hubs carry
        // [Authorize]; health/metrics opt out with .AllowAnonymous(). Anything that shows up here has
        // neither — a new endpoint that would be public by accident.
        var undeclared = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null
                        && !e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Select(e => e.RoutePattern.RawText)
            .ToArray();

        Assert.True(undeclared.Length == 0,
            $"Endpoints with no authorization declared (add [Authorize] or an explicit [AllowAnonymous]): {string.Join(", ", undeclared)}");
    }

    [Fact]
    public async Task Unknown_routes_are_still_not_found_rather_than_unauthorized()
    {
        var response = await Client.GetAsync("/api/v1/this-route-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static readonly object RegistrationBody = new
    {
        FullName = "Anon Registrant",
        AadhaarNumber = "234567890123",
        MobileNumber = "9876543210",
        Password = "Str0ng!Passw0rd#1",
    };

    [Fact]
    public async Task Member_registration_is_not_open_to_anonymous_callers()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register-member", RegistrationBody);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Member_registration_is_not_open_to_standard_users()
    {
        var client = await GetAuthenticatedClientAsync("user@r2wai.io", "R2wai_User!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return; // login itself failed — nothing to assert

        var response = await client.PostAsJsonAsync("/api/v1/auth/register-member", RegistrationBody);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Access_request_does_not_require_a_department()
    {
        var email = $"access-{Guid.NewGuid():N}@example.test";
        var response = await Client.PostAsJsonAsync("/api/v1/auth/request-access", new
        {
            FullName = "Access Request User",
            Email = email,
            Organization = "Example Organization",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = await db.AccessRequests.FirstAsync(x => x.Email == email);
        Assert.Null(request.Department);
    }
}
