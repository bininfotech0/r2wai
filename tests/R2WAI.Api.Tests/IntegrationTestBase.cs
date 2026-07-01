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

    protected async Task<string> GetAuthTokenAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "admin@r2wai.io",
            Password = "R2wai_Admin!2026"
        });

        if (!response.IsSuccessStatusCode)
            return string.Empty;

        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        return result?.Token ?? string.Empty;
    }

    protected async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var token = await GetAuthTokenAsync();
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
