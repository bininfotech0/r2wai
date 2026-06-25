using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using R2WAI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace R2WAI.Api.Tests.Integration;

[Trait("Category", "Integration")]
public class PostgresIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private bool _dockerAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16")
                .WithDatabase("r2wai_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();

            await _postgres.StartAsync();
            _dockerAvailable = true;
        }
        catch
        {
            _dockerAvailable = false;
            return;
        }

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Authentication:Jwt:SecretKey",
                    "TestingSecretKeyForIntegrationTestsThatIsLongEnough!");
                builder.UseSetting("Security:EncryptionKey",
                    Convert.ToBase64String(new byte[32]));
                builder.UseSetting("ConnectionStrings:Redis", "");
                builder.UseSetting("Cache:Redis:ConnectionString", "");
                builder.UseSetting("ConnectionStrings:DefaultConnection",
                    _postgres.GetConnectionString());

                builder.ConfigureServices(services =>
                {
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

                    var npgsqlDescriptors = services.Where(d =>
                        d.ServiceType.FullName?.Contains("Npgsql") == true ||
                        d.ImplementationType?.FullName?.Contains("Npgsql") == true
                    ).ToList();

                    foreach (var d in npgsqlDescriptors)
                        services.Remove(d);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseNpgsql(_postgres.GetConnectionString());
                        options.ConfigureWarnings(w => w.Ignore(
                            Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                    });
                    services.AddScoped<ITenantDbContext>(sp =>
                        sp.GetRequiredService<ApplicationDbContext>());
                });
            });

        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await ApplicationDbContextSeed.SeedAsync(db);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    private bool ShouldSkip()
    {
        return !_dockerAvailable;
    }

    private async Task<string> GetAuthTokenAsync()
    {
        if (ShouldSkip()) return string.Empty;
        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "admin@r2wai.io",
            Password = "admin123"
        });

        if (!response.IsSuccessStatusCode) return string.Empty;

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("token").GetString() ?? string.Empty;
    }

    private async Task<HttpClient> GetAuthClientAsync()
    {
        var token = await GetAuthTokenAsync();
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsToken()
    {
        if (ShouldSkip()) return;
        var response = await _client!.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "admin@r2wai.io",
            Password = "admin123"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("token", body);
        Assert.Contains("refreshToken", body);
    }

    [Fact]
    public async Task RefreshToken_ReturnsNewPair()
    {
        if (ShouldSkip()) return;
        var loginResponse = await _client!.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "admin@r2wai.io",
            Password = "admin123"
        });

        var loginBody = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var accessToken = loginBody.RootElement.GetProperty("token").GetString()!;
        var refreshToken = loginBody.RootElement.GetProperty("refreshToken").GetString()!;

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task GetAdminUsers_ReturnsSeededAdmin()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();
        var response = await client.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("admin@r2wai.io", body);
    }

    [Fact]
    public async Task GetAdminRoles_ReturnsSeededRoles()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();
        var response = await client.GetAsync("/api/v1/admin/roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Admin", body);
    }

    [Fact]
    public async Task GetAdminModels_ReturnsSeededModel()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();
        var response = await client.GetAsync("/api/v1/admin/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("GPT-4o", body);
    }

    [Fact]
    public async Task CreateAssistant_ThenGetList_ReturnsIt()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/v1/assistants", new
        {
            Name = "PG Test Assistant",
            Type = "General",
            SystemPrompt = "You are a test."
        });
        Assert.True(createResponse.IsSuccessStatusCode,
            $"Create failed: {createResponse.StatusCode}");

        var listResponse = await client.GetAsync("/api/v1/assistants?search=PG+Test");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var body = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains("PG Test Assistant", body);
    }

    [Fact]
    public async Task CreateChatbot_ThenGetList_ReturnsIt()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/v1/chatbots", new
        {
            Name = "PG Test Chatbot",
            Description = "Test"
        });
        Assert.True(createResponse.IsSuccessStatusCode,
            $"Create failed: {createResponse.StatusCode}");

        var listResponse = await client.GetAsync("/api/v1/chatbots");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var body = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains("PG Test Chatbot", body);
    }

    [Fact]
    public async Task CreateConversation_ThenGetList_Works()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/v1/chat/conversations", new
        {
            Title = "PG Test Chat",
            Module = "general"
        });
        Assert.True(createResponse.IsSuccessStatusCode,
            $"Create failed: {createResponse.StatusCode}");

        var listResponse = await client.GetAsync("/api/v1/chat/conversations");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
    }

    [Fact]
    public async Task GetApprovals_WithRealDb_ReturnsOk()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();
        var response = await client.GetAsync("/api/v1/approvals");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetApprovalPolicies_WithRealDb_ReturnsOk()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();
        var response = await client.GetAsync("/api/v1/approvals/policies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateWorkflow_ThenGetList_Works()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/v1/workflows", new
        {
            Name = "PG Test Workflow",
            Type = "Sequential"
        });
        Assert.True(createResponse.IsSuccessStatusCode,
            $"Create failed: {createResponse.StatusCode}");

        var listResponse = await client.GetAsync("/api/v1/workflows?search=PG+Test");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var body = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains("PG Test Workflow", body);
    }

    [Fact]
    public async Task GetUsageAnalytics_WithRealDb_ReturnsMetrics()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();
        var response = await client.GetAsync("/api/v1/admin/analytics/usage?days=30");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("period", out _));
        Assert.True(doc.RootElement.TryGetProperty("assistants", out _));
    }

    [Fact]
    public async Task ExportAuditLogs_Csv_ReturnsFile()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();
        var response = await client.GetAsync("/api/v1/operations/audit-logs/export?format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task InviteUser_DoesNotLeakToken()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/admin/users/invite", new
        {
            Email = "newinvite@test.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("inviteToken", body);
    }

    [Fact]
    public async Task UpdateProfile_Succeeds()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();
        var response = await client.PutAsJsonAsync("/api/v1/auth/profile", new
        {
            FirstName = "Updated",
            LastName = "Admin"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var meResponse = await client.GetAsync("/api/v1/auth/me");
        var body = await meResponse.Content.ReadAsStringAsync();
        Assert.Contains("Updated", body);
    }

    [Fact]
    public async Task OperationsHealth_ReportsDbStatus()
    {
        if (ShouldSkip()) return;
        var client = await GetAuthClientAsync();
        var response = await client.GetAsync("/api/v1/operations/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.Equal("healthy", status);
    }
}
