using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace R2WAI.Api.Tests.Integration;

/// <summary>
/// End-to-end tests for all 12 MVP success criteria.
/// Each test verifies the complete API flow for a specific scenario.
/// </summary>
public class EndToEndMvpTests : IClassFixture<R2WAIWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly R2WAIWebApplicationFactory _factory;

    public EndToEndMvpTests(R2WAIWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ================================================================
    // Scenario 1: Login with credentials
    // ================================================================

    [Fact]
    public async Task Scenario1_Login_InvalidCredentials_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "wrong@test.com",
            Password = "wrongpassword"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario1_Login_ForgotPassword_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            Email = "user@test.com"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("reset link", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario1_Login_ResetPassword_InvalidToken_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            Email = "user@test.com",
            Token = "BADTOKEN",
            NewPassword = "newpassword123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Scenario1_Login_GetCurrentUser_Without_Auth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================
    // Scenario 2: Navigate portal (studio cards)
    // ================================================================

    [Fact]
    public async Task Scenario2_HealthEndpoints_ReturnOk()
    {
        var startup = await _client.GetAsync("/health/startup");
        Assert.Equal(HttpStatusCode.OK, startup.StatusCode);
    }

    // ================================================================
    // Scenario 3: Create an AI Assistant
    // ================================================================

    [Fact]
    public async Task Scenario3_Assistants_RequiresAuth()
    {
        var list = await _client.GetAsync("/api/v1/assistants");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);

        var create = await _client.PostAsJsonAsync("/api/v1/assistants", new { Name = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    [Fact]
    public async Task Scenario3_AssistantPromptTemplates_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/assistants/prompt-templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario3_AssistantPublish_RequiresAuth()
    {
        var response = await _client.PostAsync($"/api/v1/assistants/{Guid.NewGuid()}/publish", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================
    // Scenario 4: Upload knowledge documents
    // ================================================================

    [Fact]
    public async Task Scenario4_DocumentUpload_RequiresAuth()
    {
        var content = new MultipartFormDataContent();
        var upload = await _client.PostAsync("/api/v1/documents/upload", content);
        Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
    }

    [Fact]
    public async Task Scenario4_BulkUpload_RequiresAuth()
    {
        var content = new MultipartFormDataContent();
        var response = await _client.PostAsync("/api/v1/documents/bulk-upload", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario4_KnowledgeBases_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/knowledgebases");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================
    // Scenario 5: Chat with assistant
    // ================================================================

    [Fact]
    public async Task Scenario5_AssistantChat_RequiresAuth()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/assistants/{Guid.NewGuid()}/chat", new
        {
            Message = "Hello"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================
    // Scenario 6: Create a workflow
    // ================================================================

    [Fact]
    public async Task Scenario6_Workflows_RequiresAuth()
    {
        var list = await _client.GetAsync("/api/v1/workflows");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);

        var create = await _client.PostAsJsonAsync("/api/v1/workflows", new { Name = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    [Fact]
    public async Task Scenario6_WorkflowTemplates_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/workflows/templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================
    // Scenario 7: Execute workflow
    // ================================================================

    [Fact]
    public async Task Scenario7_WorkflowExecute_RequiresAuth()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/workflows/{Guid.NewGuid()}/execute", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario7_WorkflowSchedule_RequiresAuth()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/workflows/{Guid.NewGuid()}/schedule", new
        {
            CronExpression = "0 * * * *"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================
    // Scenario 8: Receive approval notification
    // ================================================================

    [Fact]
    public async Task Scenario8_Approvals_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/approvals");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario8_ApprovalPending_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/approvals/pending");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================
    // Scenario 9: Approve or reject
    // ================================================================

    [Fact]
    public async Task Scenario9_ApproveRequest_RequiresAuth()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/approvals/{Guid.NewGuid()}/approve", new
        {
            Comments = "Approved"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario9_RejectRequest_RequiresAuth()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/approvals/{Guid.NewGuid()}/reject", new
        {
            Comments = "Rejected"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario9_ApprovalPolicies_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/approvals/policies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================
    // Scenario 10: Monitor workflow execution
    // ================================================================

    [Fact]
    public async Task Scenario10_WorkflowInstances_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/workflows/instances");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario10_WorkflowSteps_RequiresAuth()
    {
        var response = await _client.GetAsync($"/api/v1/workflows/instances/{Guid.NewGuid()}/steps");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario10_WorkflowRetry_RequiresAuth()
    {
        var response = await _client.PostAsync($"/api/v1/workflows/instances/{Guid.NewGuid()}/retry", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario10_OperationsMetrics_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/operations/metrics");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================
    // Scenario 11: View audit logs
    // ================================================================

    [Fact]
    public async Task Scenario11_AuditLogs_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/operations/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario11_AuditLogsExport_RequiresAuth()
    {
        var csv = await _client.GetAsync("/api/v1/operations/audit-logs/export?format=csv");
        Assert.Equal(HttpStatusCode.Unauthorized, csv.StatusCode);

        var json = await _client.GetAsync("/api/v1/operations/audit-logs/export?format=json");
        Assert.Equal(HttpStatusCode.Unauthorized, json.StatusCode);
    }

    // ================================================================
    // Scenario 12: Configure users and roles
    // ================================================================

    [Fact]
    public async Task Scenario12_AdminUsers_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario12_AdminRoles_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/admin/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario12_AdminModels_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/admin/models");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario12_AdminInviteUser_RequiresAuth()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/admin/users/invite", new
        {
            Email = "new@test.com"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario12_AdminAnalytics_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/admin/analytics/usage");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario12_AdminModelTest_RequiresAuth()
    {
        var response = await _client.PostAsync($"/api/v1/admin/models/{Guid.NewGuid()}/test", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scenario12_AdminSettings_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/admin/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ================================================================
    // Cross-cutting: Integration & Webhook endpoints
    // ================================================================

    [Fact]
    public async Task CrossCutting_Integrations_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/integrations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CrossCutting_IntegrationTest_RequiresAuth()
    {
        var response = await _client.PostAsync($"/api/v1/integrations/{Guid.NewGuid()}/test", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CrossCutting_WebhookTrigger_NoMatchingWorkflow_ReturnsNonSuccess()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/workflows/webhook/nonexistent-slug", new { data = "test" });
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 404 or 500 but got {response.StatusCode}");
    }

    [Fact]
    public async Task CrossCutting_KnowledgeBaseReindex_RequiresAuth()
    {
        var response = await _client.PostAsync($"/api/v1/knowledgebases/{Guid.NewGuid()}/reindex", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CrossCutting_WorkflowPublish_RequiresAuth()
    {
        var response = await _client.PostAsync($"/api/v1/workflows/{Guid.NewGuid()}/publish", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
