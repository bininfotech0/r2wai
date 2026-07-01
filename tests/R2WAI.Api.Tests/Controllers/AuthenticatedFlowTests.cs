using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Controllers;

public class AuthenticatedFlowTests : IntegrationTestBase
{
    public AuthenticatedFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    private async Task<string?> LoginAndGetTokenAsync()
    {
        await Factory.EnsureSeededAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "admin@r2wai.io",
            Password = "R2wai_Admin!2026"
        });

        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("token").GetString();
    }

    [Fact]
    public async Task SeedData_CreatesAdminUser()
    {
        await Factory.EnsureSeededAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userCount = db.Users.IgnoreQueryFilters().Count(u => !u.IsDeleted);
        Assert.True(userCount > 0, "Seed data should create at least one user");

        var admin = db.Users.IgnoreQueryFilters()
            .FirstOrDefault(u => u.Email == "admin@r2wai.io" && !u.IsDeleted);
        Assert.NotNull(admin);
    }

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsToken()
    {
        var token = await LoginAndGetTokenAsync();
        Assert.False(string.IsNullOrEmpty(token), "Login should return a valid JWT token");
    }

    [Fact]
    public async Task GetMe_WithToken_ReturnsUserInfo()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("admin@r2wai.io", body);
    }

    [Fact]
    public async Task UpdateProfile_WithToken_Succeeds()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync("/api/v1/auth/profile", new
        {
            FirstName = "Updated",
            LastName = "Admin"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithToken_Succeeds()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAssistants_WithToken_ReturnsOk()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/assistants");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateAssistant_WithToken_ReturnsCreated()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v1/assistants", new
        {
            Name = "Test Assistant",
            Type = "General",
            SystemPrompt = "You are a test assistant."
        });
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Expected Created/OK but got {response.StatusCode}");
    }

    [Fact]
    public async Task GetChatbots_WithToken_ReturnsOk()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/chatbots");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateChatbot_WithToken_ReturnsCreated()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v1/chatbots", new
        {
            Name = "Test Chatbot",
            Description = "A test chatbot"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Expected Created/OK but got {response.StatusCode}");
    }

    [Fact]
    public async Task GetConversations_WithToken_ReturnsOk()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/chat/conversations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminUsers_WithToken_IsNotUnauthorized()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/users");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminRoles_WithToken_IsNotUnauthorized()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/roles");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminModels_WithToken_IsNotUnauthorized()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/models");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetApprovals_WithToken_IsNotUnauthorized()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/approvals");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditLogs_WithToken_ReturnsOk()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/operations/audit-logs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOperationsHealth_WithToken_ReturnsStatus()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping authenticated test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/operations/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task RefreshToken_AfterLogin_ReturnsNewTokenPair()
    {
        await Factory.EnsureSeededAsync();

        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "admin@r2wai.io",
            Password = "R2wai_Admin!2026"
        });

        if (!loginResponse.IsSuccessStatusCode)
        {
            Assert.Fail("Could not login — skipping refresh test");
            return;
        }

        var loginBody = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var accessToken = loginBody.RootElement.GetProperty("token").GetString()!;
        var refreshToken = loginBody.RootElement.GetProperty("refreshToken").GetString()!;

        var refreshResponse = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshBody = JsonDocument.Parse(await refreshResponse.Content.ReadAsStringAsync());
        Assert.True(refreshBody.RootElement.TryGetProperty("token", out _));
        Assert.True(refreshBody.RootElement.TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task ExportAuditLogs_WithToken_ReturnsCsv()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping export test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/operations/audit-logs/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ExportAuditLogs_WithToken_ReturnsJson()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping export test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/operations/audit-logs/export?format=json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task InviteUser_WithToken_IsNotUnauthorized()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping invite test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v1/admin/users/invite", new
        {
            Email = "newuser@test.com"
        });

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("inviteToken", body);
        }
    }

    [Fact]
    public async Task CreateConversation_WithToken_ReturnsCreated()
    {
        var token = await LoginAndGetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Assert.Fail("Could not obtain auth token — skipping conversation test");
            return;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v1/chat/conversations", new
        {
            Title = "Test Conversation",
            Module = "general"
        });

        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Expected Created/OK but got {response.StatusCode}");
    }
}
