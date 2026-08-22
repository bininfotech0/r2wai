using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests;

public class IntegrationTestBase : IClassFixture<R2WAIWebApplicationFactory>
{
    protected readonly HttpClient Client;
    protected readonly R2WAIWebApplicationFactory Factory;

    public IntegrationTestBase(R2WAIWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected Task<string> GetAuthTokenAsync() => GetAuthTokenAsync("admin@r2wai.io", "R2wai_Admin!2026");

    protected async Task<string> GetAuthTokenAsync(string email, string password)
    {
        await Factory.EnsureSeededAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = password
        });

        if (!response.IsSuccessStatusCode)
            return string.Empty;

        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        return result?.Token ?? string.Empty;
    }

    protected Task<HttpClient> GetAuthenticatedClientAsync() => GetAuthenticatedClientAsync("admin@r2wai.io", "R2wai_Admin!2026");

    // "user@r2wai.io" / "R2wai_User!2026" is the seeded plain-User-role account (ApplicationDbContextSeed)
    // — use this to exercise role-matrix negative tests (asserting a non-admin is actually denied),
    // as opposed to the admin overload every other flow test uses.
    protected async Task<HttpClient> GetAuthenticatedClientAsync(string email, string password)
    {
        var token = await GetAuthTokenAsync(email, password);
        var client = Factory.CreateClient();
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private class LoginResult
    {
        public string Token { get; set; } = string.Empty;
    }
}

public class R2WAIWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"R2WAI_Tests_{Guid.NewGuid()}";
    private bool _seeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Authentication:Jwt:SecretKey", "TestingSecretKeyForUnitTestsThatIsLongEnough!");
        // EncryptionService requires a 32-byte key or throws from its constructor — and since
        // AdminController takes IEncryptionService as a constructor parameter, every AdminController
        // action fails before it even runs without this, not just the ones that actually encrypt
        // anything. Live deployments set this via docker-compose's ENCRYPTION_KEY; the test host
        // never did, so nothing exercising AdminController beyond a 401-unauthenticated check could
        // have caught it. Must be a real environment variable, not just config: EncryptionService
        // explicitly refuses a config-only key outside Development, and this host runs as "Testing".
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", "lEq8IPYv6Hd2+m2OX+kjWGsx4NIhsX4COeYDKiR0D2M=");

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related registrations to avoid dual provider conflict
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType.FullName?.Contains("DbContextOptions") == true ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                d.ImplementationType == typeof(ApplicationDbContext) ||
                d.ServiceType == typeof(ITenantDbContext) ||
                d.ServiceType == typeof(IHostedService)
            ).ToList();

            foreach (var d in toRemove)
                services.Remove(d);

            // Also remove Npgsql-specific services that conflict with InMemory
            var npgsqlDescriptors = services.Where(d =>
                d.ServiceType.FullName?.Contains("Npgsql") == true ||
                d.ImplementationType?.FullName?.Contains("Npgsql") == true
            ).ToList();

            foreach (var d in npgsqlDescriptors)
                services.Remove(d);

            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.ConfigureWarnings(w =>
                {
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                });
            });
            services.AddScoped<ITenantDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        });
    }

    public async Task EnsureSeededAsync()
    {
        if (_seeded) return;
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await ApplicationDbContextSeed.SeedAsync(context);
        _seeded = true;
    }
}
