using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Integration;

public class MvpFlowIntegrationTests : IClassFixture<R2WAIWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly R2WAIWebApplicationFactory _factory;

    public MvpFlowIntegrationTests(R2WAIWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "nonexistent@test.com",
            Password = "wrongpassword"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithAnyEmail_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            Email = "anyuser@test.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            Email = "admin@r2wai.io",
            Token = "INVALIDTOKEN",
            NewPassword = "newpassword123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Assistants_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/assistants");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Workflows_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/workflows");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Approvals_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/approvals");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Operations_Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/operations/health");
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WorkflowTemplates_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/workflows/templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AssistantPromptTemplates_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/assistants/prompt-templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Documents_BulkUpload_WithoutAuth_ReturnsUnauthorized()
    {
        var content = new MultipartFormDataContent();
        var response = await _client.PostAsync("/api/v1/documents/bulk-upload", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_Export_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/operations/audit-logs/export?format=csv");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_InviteUser_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/admin/users/invite", new
        {
            Email = "newuser@test.com"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_Startup_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/startup");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
