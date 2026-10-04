using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Controllers;

public class IntegrationFlowTests : IntegrationTestBase
{
    private static readonly Guid SeededTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public IntegrationFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    private async Task<Guid> SeedGovernedToolAsync(string? requiredRole, bool approvalRequired = false)
    {
        await Factory.EnsureSeededAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tool = new ToolDefinition(Guid.NewGuid(), SeededTenantId, $"governance_probe_{Guid.NewGuid():N}",
            ToolType.Http, "Governance bypass regression probe", "https://example.invalid/test");
        tool.ConfigureGovernance("High", requiredRole, confirmationRequired: false, approvalRequired, auditRequired: true);
        db.Set<ToolDefinition>().Add(tool);
        await db.SaveChangesAsync();
        return tool.Id;
    }

    [Fact]
    public async Task GetCatalog_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/integrations/catalog");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCatalog_WithAuth_ReturnsNonEmptyCuratedList()
    {
        var client = await GetAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/integrations/catalog");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        Assert.NotNull(entries);
        Assert.NotEmpty(entries);
        // Only Http is real end to end today (see GetIntegrationCatalogQueryHandler's comment) —
        // cataloguing any other type would point an admin at a dead end.
        Assert.All(entries!, e => Assert.Equal("Http", e.GetProperty("suggestedType").GetString()));
    }

    [Fact]
    public async Task TestIntegration_UserMissingRequiredRole_Returns403WithoutAttemptingConnection()
    {
        // Regression guard for the confirmed governance-bypass gap: this endpoint used to call
        // DynamicToolExecutor directly, skipping AiFunctionAuditFilter's RequiredRole/ApprovalRequired/
        // RiskLevel checks entirely — so a tool gated to SystemAdmin was still fully testable by any
        // authenticated tenant user. The endpoint is invalid (example.invalid) — a 403 here proves
        // governance denied the call before any connection attempt was even made.
        var toolId = await SeedGovernedToolAsync(requiredRole: "SystemAdmin");

        var client = await GetAuthenticatedClientAsync("user@r2wai.io", "R2wai_User!2026");
        var response = await client.PostAsync($"/api/v1/integrations/{toolId}/test", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestIntegration_ApprovalRequired_Returns403WithoutAttemptingConnection()
    {
        var toolId = await SeedGovernedToolAsync(requiredRole: null, approvalRequired: true);

        var client = await GetAuthenticatedClientAsync();
        var response = await client.PostAsync($"/api/v1/integrations/{toolId}/test", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestIntegration_UngovernedTool_PassesGovernanceAndAttemptsConnection()
    {
        // Negative control: no RequiredRole/ApprovalRequired set — must NOT 403. Proves the fix is a
        // real governance check, not an accidental blanket-deny of this endpoint.
        var toolId = await SeedGovernedToolAsync(requiredRole: null, approvalRequired: false);

        var client = await GetAuthenticatedClientAsync("user@r2wai.io", "R2wai_User!2026");
        var response = await client.PostAsync($"/api/v1/integrations/{toolId}/test", null);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetIntegrations_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/integrations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateIntegration_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/integrations", new
        {
            Name = "Test",
            Type = "HTTP"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ToggleIntegration_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/integrations/{Guid.NewGuid()}/toggle", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestIntegration_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsync($"/api/v1/integrations/{Guid.NewGuid()}/test", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
