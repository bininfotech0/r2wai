using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Controllers;

/// <summary>
/// Implementation plan Phase 3 — the MCP server allowlist + discover/commit two-step, mirroring
/// IntegrationFlowTests' coverage of IntegrationsController's own analyze/commit + auth gates.
/// No real MCP server exists in this harness, so Test/Discover are only exercised against a
/// deliberately-blocked (private) endpoint — proving EgressGuard denies before any connection is
/// attempted, the same bar IntegrationFlowTests holds DynamicToolExecutor to.
/// </summary>
public class McpConnectionsControllerTests : IntegrationTestBase
{
    private static readonly Guid SeededTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public McpConnectionsControllerTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    private async Task<Guid> SeedConnectionAsync(string endpointUrl, string? credentialEncrypted = null)
    {
        await Factory.EnsureSeededAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = new McpServerConnection(Guid.NewGuid(), SeededTenantId, "Seeded MCP Server", endpointUrl,
            authHeaderName: null, credentialEncrypted: credentialEncrypted);
        db.Set<McpServerConnection>().Add(connection);
        await db.SaveChangesAsync();
        return connection.Id;
    }

    [Fact]
    public async Task GetList_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/mcp-connections");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/mcp-connections", new
        {
            Name = "Test", EndpointUrl = "https://mcp.example.com"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_PrivateOrInternalEndpoint_Returns400_NeverPersists()
    {
        var client = await GetAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/mcp-connections", new
        {
            Name = "Blocked", EndpointUrl = "http://169.254.169.254/mcp"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // IgnoreQueryFilters: this raw scope has no ambient authenticated HttpContext, so the fail-closed
        // tenant filter (ApplicationDbContext.ApplyTenantFilter) would otherwise hide every row regardless
        // of whether Create actually persisted one — see TenantIsolationFailClosedTests for the same point.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<McpServerConnection>().IgnoreQueryFilters().AnyAsync(c => c.Name == "Blocked"));
    }

    [Fact]
    public async Task Create_ValidEndpoint_PersistsWithCredentialEncrypted_NotPlaintext()
    {
        var client = await GetAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/mcp-connections", new
        {
            Name = "Real Server", EndpointUrl = "https://mcp.example.com", AuthHeaderName = "X-Api-Key", Credential = "super-secret-value"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.True(body.GetProperty("hasCredential").GetBoolean());
        // Response DTO never echoes the credential in any form.
        Assert.False(body.TryGetProperty("credential", out _));
        Assert.False(body.TryGetProperty("credentialEncrypted", out _));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Set<McpServerConnection>().IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        Assert.NotNull(stored.CredentialEncrypted);
        Assert.DoesNotContain("super-secret-value", stored.CredentialEncrypted!);
    }

    [Fact]
    public async Task GetById_ConnectionBelongsToAnotherTenant_Returns404_HidesExistenceRatherThanLeakingIt()
    {
        // The fail-closed tenant filter already excludes another tenant's row from the query the
        // controller's own GetOwnedConnectionAsync issues, so this hits the NotFoundException branch
        // before its manual TenantId check ever runs — same defense-in-depth shape as
        // IntegrationsController.Test's own cross-tenant check. 404 (not 401/403) is the correct,
        // safer response here: it doesn't confirm to the caller that a resource they can't access
        // exists at all.
        await Factory.EnsureSeededAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = new McpServerConnection(Guid.NewGuid(), Guid.NewGuid(), "Other Tenant's Server", "https://mcp.example.com");
        db.Set<McpServerConnection>().Add(connection);
        await db.SaveChangesAsync();

        var client = await GetAuthenticatedClientAsync();
        var response = await client.GetAsync($"/api/v1/mcp-connections/{connection.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Test_ConnectionTargetsAPrivateAddress_ReturnsUnprocessable_RecordsErrorStatus()
    {
        var connectionId = await SeedConnectionAsync("http://10.0.0.5/mcp");
        var client = await GetAuthenticatedClientAsync();

        var response = await client.PostAsync($"/api/v1/mcp-connections/{connectionId}/test", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Set<McpServerConnection>().IgnoreQueryFilters().SingleAsync(c => c.Id == connectionId);
        Assert.Equal("Error", stored.LastTestStatus);
    }

    [Fact]
    public async Task Discover_ConnectionTargetsAPrivateAddress_Returns400_NeverPersistsAnyToolDefinition()
    {
        var connectionId = await SeedConnectionAsync("http://169.254.169.254/mcp");
        var client = await GetAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/v1/mcp-connections/{connectionId}/discover");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Commit_CreatedToolDefinitions_AreInactive_NotAutoActivated()
    {
        var connectionId = await SeedConnectionAsync("https://mcp.example.com");
        var client = await GetAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/mcp-connections/{connectionId}/commit", new
        {
            Tools = new[] { new { Name = "get_status", Description = "Gets status" } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = body.GetProperty("ids")[0].GetGuid();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tool = await db.Set<ToolDefinition>().IgnoreQueryFilters().SingleAsync(t => t.Id == id);
        Assert.False(tool.IsActive);
        Assert.Equal(connectionId, tool.McpServerConnectionId);
        Assert.Equal("get_status", tool.McpToolName);
    }

    [Fact]
    public async Task Delete_WithoutAuth_Returns401()
    {
        var response = await Client.DeleteAsync($"/api/v1/mcp-connections/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
