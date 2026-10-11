using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Controllers;

/// <summary>
/// GET /runs now includes the ToolExecution ledger as "ToolCall" rows. Tool calls are governance
/// data, so a tenant admin sees everyone's while a plain user sees only the calls their own AI made.
/// </summary>
public class RunsToolCallsTests : IntegrationTestBase
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid StandardUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public RunsToolCallsTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    private async Task<(ToolExecution AdminCall, ToolExecution UserCall)> SeedAsync()
    {
        await Factory.EnsureSeededAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var adminCall = ToolExecution.Denied(TenantId, AdminId, null, "Rogue", $"admin_call_{marker}", "not a registered, governed tool");
        var userCall = ToolExecution.Prepared(TenantId, StandardUserId, null, "DynamicTools", $"user_call_{marker}");
        userCall.Succeed(42);

        using var scope = SignedInScope(TenantId, AdminId);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ToolExecutions.AddRange(adminCall, userCall);
        await db.SaveChangesAsync();
        return (adminCall, userCall);
    }

    private static async Task<List<string>> ToolCallIdsAsync(HttpClient client, string query = "")
    {
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/runs?type=ToolCall&pageSize=100{query}");
        return body.GetProperty("items").EnumerateArray()
            .Select(item =>
            {
                Assert.Equal("ToolCall", item.GetProperty("type").GetString());
                return item.GetProperty("id").GetString()!;
            })
            .ToList();
    }

    [Fact]
    public async Task Admin_sees_every_users_tool_calls_with_their_status()
    {
        var (adminCall, userCall) = await SeedAsync();
        var client = await GetAuthenticatedClientAsync();

        var ids = await ToolCallIdsAsync(client);

        Assert.Contains(adminCall.Id.ToString(), ids);
        Assert.Contains(userCall.Id.ToString(), ids);
        Assert.Contains(userCall.Id.ToString(), await ToolCallIdsAsync(client, "&status=Succeeded"));
        Assert.DoesNotContain(adminCall.Id.ToString(), await ToolCallIdsAsync(client, "&status=Succeeded"));
    }

    [Fact]
    public async Task A_plain_user_sees_only_their_own_tool_calls_and_cannot_open_anyone_elses()
    {
        var (adminCall, userCall) = await SeedAsync();
        var client = await GetAuthenticatedClientAsync("user@r2wai.io", "R2wai_User!2026");

        var ids = await ToolCallIdsAsync(client);

        Assert.Contains(userCall.Id.ToString(), ids);
        Assert.DoesNotContain(adminCall.Id.ToString(), ids);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/runs/tool-calls/{adminCall.Id}")).StatusCode);
    }

    [Fact]
    public async Task Tool_call_detail_returns_the_ledger_row()
    {
        var (adminCall, _) = await SeedAsync();
        var client = await GetAuthenticatedClientAsync();

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/runs/tool-calls/{adminCall.Id}");

        Assert.Equal("Denied", detail.GetProperty("status").GetString());
        Assert.Equal("not a registered, governed tool", detail.GetProperty("denialReason").GetString());
        Assert.Equal(adminCall.Function, detail.GetProperty("function").GetString());
    }

    [Fact]
    public async Task Filtering_to_another_run_type_excludes_tool_calls()
    {
        var (adminCall, _) = await SeedAsync();
        var client = await GetAuthenticatedClientAsync();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/runs?type=Automation&pageSize=100");

        Assert.DoesNotContain(body.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetString() == adminCall.Id.ToString());
    }
}
