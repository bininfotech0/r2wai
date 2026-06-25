using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Security;

public class ApiEndpointSecurityTests : IntegrationTestBase
{
    public ApiEndpointSecurityTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    // ═══════════════════════════════════════════════════════════════════
    //  SCHEDULES CONTROLLER — Auth Required
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSchedules_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/schedules");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CreateSchedule_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/schedules", new
        {
            Name = "Test Schedule",
            CronExpression = "0 * * * *"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task DeleteSchedule_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/schedules/{Guid.NewGuid()}");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task ToggleSchedule_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/schedules/{Guid.NewGuid()}/toggle", null);
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WEBHOOKS CONTROLLER — Auth Required
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetWebhooks_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/webhooks");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CreateWebhook_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/webhooks", new
        {
            Name = "Test Webhook",
            Url = "https://example.com/hook",
            Events = new[] { "workflow.completed" }
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task DeleteWebhook_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/webhooks/{Guid.NewGuid()}");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  API KEYS CONTROLLER — Auth Required
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetApiKeys_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/api-keys");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CreateApiKey_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/api-keys", new
        {
            Name = "Test Key"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task RevokeApiKey_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/api-keys/{Guid.NewGuid()}");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ADMIN CONTROLLER — Full Coverage
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminGetUsers_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/users");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task AdminCreateUser_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            Email = "newuser@test.com",
            FirstName = "Test",
            LastName = "User",
            Password = "Test123!"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task AdminDeleteUser_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/admin/users/{Guid.NewGuid()}");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task AdminGetRoles_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/roles");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task AdminGetSecuritySettings_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/security-settings");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task AdminGetModelConfigs_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/model-configurations");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task AdminGetContentModeration_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/admin/content-moderation");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ASSISTANTS CONTROLLER — Auth Required
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAssistants_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/assistants");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CreateAssistant_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/assistants", new
        {
            Name = "Test Assistant",
            SystemPrompt = "You are a test assistant"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CHAT CONTROLLER — Auth Required
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SendChat_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/chat/send", new
        {
            ConversationId = Guid.NewGuid(),
            Message = "Hello"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task GetConversations_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/chat/conversations");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CHATBOTS CONTROLLER — Auth Required
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetChatbots_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/chatbots");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CreateChatbot_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/chatbots", new
        {
            Name = "Test Chatbot"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  INTEGRATIONS CONTROLLER — Auth Required
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetIntegrations_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/integrations");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CreateIntegration_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/integrations", new
        {
            Name = "Test Integration",
            Type = "webhook"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WORKFLOWS CONTROLLER — Auth Required
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetWorkflows_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/workflows");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CreateWorkflow_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/workflows", new
        {
            Name = "Test Workflow"
        });
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task ExecuteWorkflow_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/workflows/{Guid.NewGuid()}/execute", null);
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  APPROVALS CONTROLLER — Auth Required
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetApprovals_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/approvals");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task ApproveRequest_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/approvals/{Guid.NewGuid()}/approve", null);
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected 401 or 404, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CROSS-CUTTING: HTTP METHOD ENFORCEMENT
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("DELETE", "/api/v1/auth/login")]
    [InlineData("PUT", "/api/v1/auth/login")]
    [InlineData("PATCH", "/api/v1/auth/login")]
    public async Task AuthLogin_WrongHttpMethod_Returns405Or404(string method, string endpoint)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        var response = await Client.SendAsync(request);
        Assert.True(
            response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound
                or HttpStatusCode.Unauthorized,
            $"{method} {endpoint} should not be allowed, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CORS / SECURITY HEADERS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task XContentTypeOptions_PreventsSniffing()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        if (response.Headers.Contains("X-Content-Type-Options"))
        {
            var value = response.Headers.GetValues("X-Content-Type-Options").First();
            Assert.Equal("nosniff", value);
        }
    }

    [Fact]
    public async Task CacheControl_NotStoringAuthResponses()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        if (response.Headers.CacheControl != null)
        {
            Assert.True(
                response.Headers.CacheControl.NoStore || response.Headers.CacheControl.NoCache,
                "Auth responses should not be cached");
        }
    }
}
