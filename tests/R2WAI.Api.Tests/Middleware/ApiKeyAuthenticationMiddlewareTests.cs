using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Api.Middleware;

namespace R2WAI.Api.Tests.Middleware;

/// <summary>
/// Audit finding P0-5: a statically configured API key could be defined without a TenantId; it then
/// authenticated with no tenant_id claim, and the tenant query filter treats "no tenant" as "every
/// tenant" — so such a key read across all tenants.
/// </summary>
public class ApiKeyAuthenticationMiddlewareTests
{
    private const string Key = "static-test-key-1234567890";
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    private static (ApiKeyAuthenticationMiddleware Middleware, Func<bool> WasCalled) Build(string? tenantId)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Authentication:ApiKeys:0:Key"] = Key,
            ["Authentication:ApiKeys:0:Name"] = "static test key",
            ["Authentication:ApiKeys:0:Roles:0"] = "Admin",
        };
        if (tenantId is not null)
            settings["Authentication:ApiKeys:0:TenantId"] = tenantId;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var called = false;
        var middleware = new ApiKeyAuthenticationMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            configuration,
            NullLogger<ApiKeyAuthenticationMiddleware>.Instance);
        return (middleware, () => called);
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Request.Headers["X-API-Key"] = Key;
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Static_key_without_a_valid_tenant_is_rejected(string? tenantId)
    {
        var (middleware, wasCalled) = Build(tenantId);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(wasCalled());
    }

    [Fact]
    public async Task Static_key_with_a_tenant_authenticates_and_carries_the_tenant_claim()
    {
        var (middleware, wasCalled) = Build(Tenant.ToString());
        var context = NewContext();

        await middleware.InvokeAsync(context);

        Assert.True(wasCalled());
        Assert.Equal(Tenant.ToString(), context.User.FindFirst("tenant_id")?.Value);
    }
}
