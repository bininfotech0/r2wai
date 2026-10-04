using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.Authentication;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Services;

/// <summary>
/// Audit finding P0-6. The demo seed (accounts with publicly known passwords, a second tenant) used to run
/// on any empty database in every environment except "Testing". It is opt-in now; a real deployment gets
/// only the tenant, roles, built-in tools and — when configured — one administrator.
/// </summary>
public class DatabaseBootstrapTests : IClassFixture<R2WAIWebApplicationFactory>
{
    private const string ValidPassword = "a-long-unique-passphrase-1";

    private readonly R2WAIWebApplicationFactory _factory;

    public DatabaseBootstrapTests(R2WAIWebApplicationFactory factory) => _factory = factory;

    // A brand-new, empty in-memory database wired with the host's real services.
    private ApplicationDbContext NewEmptyContext(IServiceScope scope)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("bootstrap-" + Guid.NewGuid())
            .Options;
        var sp = scope.ServiceProvider;
        return new ApplicationDbContext(
            options,
            sp.GetRequiredService<ICurrentUserService>(),
            sp.GetRequiredService<IDateTimeService>(),
            sp.GetRequiredService<IMediator>(),
            sp.GetRequiredService<ILogger<ApplicationDbContext>>());
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? seedDemoData) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(seedDemoData is null
                ? []
                : new Dictionary<string, string?> { ["Database:SeedDemoData"] = seedDemoData })
            .Build();

    [Theory]
    [InlineData("Development", null, true)]
    [InlineData("Production", null, false)]
    [InlineData("Staging", null, false)]
    [InlineData("Production", "true", true)]
    [InlineData("Development", "false", false)]
    [InlineData("Production", "not-a-bool", false)]
    public void Demo_seeding_is_opt_in_outside_development(string environment, string? setting, bool expected)
    {
        Assert.Equal(expected, DatabaseBootstrap.ShouldSeedDemoData(new FakeEnvironment(environment), Config(setting)));
    }

    [Fact]
    public async Task Bootstrap_creates_the_tenant_roles_tools_and_only_the_configured_admin()
    {
        using var scope = _factory.Services.CreateScope();
        await using var context = NewEmptyContext(scope);

        var result = await DatabaseBootstrap.SeedAsync(
            context, new BootstrapAdmin("owner@agency.example", ValidPassword), NullLogger.Instance);

        Assert.Equal(BootstrapResult.CreatedWithAdmin, result);
        var user = await context.Users.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("owner@agency.example", user.Email);
        Assert.True(new PasswordHasher().Verify(ValidPassword, user.PasswordHash!));
        // IgnoreQueryFilters: this test resolves a raw ApplicationDbContext with no ambient
        // authenticated HttpContext at all (see NewEmptyContext) — the Role navigation this query
        // joins through is tenant-filtered too, and P0-5's fail-closed change means a null ambient
        // tenant now excludes it rather than (as before) including everything.
        var roleNames = await context.UserRoles.IgnoreQueryFilters()
            .Where(ur => ur.UserId == user.Id).Select(ur => ur.Role!.Name).ToListAsync();
        Assert.Contains("Admin", roleNames);
        Assert.Contains("SystemAdmin", roleNames);
        Assert.Equal(3, await context.Roles.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await context.Tenants.CountAsync());
        Assert.Equal(14, await context.ToolDefinitions.IgnoreQueryFilters().CountAsync());
        Assert.DoesNotContain(await context.Users.IgnoreQueryFilters().Select(u => u.Email).ToListAsync(),
            email => email!.EndsWith("@r2wai.io", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Bootstrap_without_an_admin_configured_creates_no_users_at_all()
    {
        using var scope = _factory.Services.CreateScope();
        await using var context = NewEmptyContext(scope);

        var result = await DatabaseBootstrap.SeedAsync(context, new BootstrapAdmin(null, null), NullLogger.Instance);

        Assert.Equal(BootstrapResult.CreatedWithoutAdmin, result);
        Assert.Empty(await context.Users.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(1, await context.Tenants.CountAsync());
    }

    [Theory]
    [InlineData("owner@agency.example", "short")]                       // too short
    [InlineData("owner@agency.example", "R2wai_Admin!2026")]            // published demo password
    [InlineData("owner@agency.example", "Test@1234!")]                  // published demo password
    [InlineData("not-an-email", ValidPassword)]                         // bad email
    [InlineData("", ValidPassword)]                                     // no email
    [InlineData("owner@agency.example", "")]                            // no password
    public async Task Bootstrap_rejects_a_weak_or_incomplete_admin_and_creates_no_user(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        await using var context = NewEmptyContext(scope);

        var result = await DatabaseBootstrap.SeedAsync(context, new BootstrapAdmin(email, password), NullLogger.Instance);

        Assert.Equal(BootstrapResult.CreatedWithoutAdmin, result);
        Assert.Empty(await context.Users.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Bootstrap_is_a_no_op_on_a_database_that_is_already_initialised()
    {
        using var scope = _factory.Services.CreateScope();
        await using var context = NewEmptyContext(scope);
        await DatabaseBootstrap.SeedAsync(context, new BootstrapAdmin("owner@agency.example", ValidPassword), NullLogger.Instance);

        var second = await DatabaseBootstrap.SeedAsync(
            context, new BootstrapAdmin("someone.else@agency.example", ValidPassword), NullLogger.Instance);

        Assert.Equal(BootstrapResult.AlreadyInitialized, second);
        Assert.Single(await context.Users.IgnoreQueryFilters().ToListAsync());
    }
}
