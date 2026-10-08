using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Tests.Security;

/// <summary>
/// Audit finding P0-4, driven through a real Semantic Kernel with the real filter, repositories and policy
/// services (in-memory database). Before the fix a function with no governance record was allowed, and a
/// dynamic tool whose free-text name differs from its sanitised function name ("Get Supplier" vs
/// "Get_Supplier") never matched its record by name — so its approval requirement was never applied.
/// </summary>
public class ToolGovernanceFilterTests : IClassFixture<R2WAIWebApplicationFactory>
{
    private readonly R2WAIWebApplicationFactory _factory;

    public ToolGovernanceFilterTests(R2WAIWebApplicationFactory factory) => _factory = factory;

    private sealed class Harness(IServiceScope scope, Guid tenantId) : IDisposable
    {
        public IServiceScope Scope { get; } = scope;
        public Guid TenantId { get; } = tenantId;
        public bool BodyRan { get; set; }

        public ApplicationDbContext Context => Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        public void Dispose() => Scope.Dispose();
    }

    // Signs the current async flow in as a caller of the given tenant (CurrentUserService reads the
    // ambient HttpContext), then returns a scope whose services see that caller.
    private Harness SignIn(Guid? tenant = null, params string[] roles)
    {
        var tenantId = tenant ?? Guid.NewGuid();
        var scope = _factory.Services.CreateScope();
        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return new Harness(scope, tenantId);
    }

    private async Task<string?> InvokeAsync(Harness harness, string plugin, string functionName, Guid? toolDefinitionId = null)
    {
        var function = KernelFunctionFactory.CreateFromMethod(
            () => { harness.BodyRan = true; return "tool-ran"; },
            new KernelFunctionFromMethodOptions
            {
                FunctionName = functionName,
                AdditionalMetadata = toolDefinitionId is null
                    ? null
                    : new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                        new Dictionary<string, object?> { [AiFunctionAuditFilter.ToolDefinitionIdMetadataKey] = toolDefinitionId })
            });

        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromFunctions(plugin, [function]);
        kernel.FunctionInvocationFilters.Add(harness.Scope.ServiceProvider.GetRequiredService<AiFunctionAuditFilter>());

        // Invoke the plugin-bound function, as the model's tool calls do: the original instance has no
        // plugin name, and the filter uses the plugin name to recognise the utility plugins.
        var result = await kernel.InvokeAsync(kernel.Plugins.GetFunction(plugin, functionName));
        return result.GetValue<string>();
    }

    private static ToolDefinition RegisteredTool(Guid tenantId, string name, string risk = "Low", bool approvalRequired = false)
    {
        var tool = new ToolDefinition(Guid.NewGuid(), tenantId, name, ToolType.Http, "test tool", "https://api.example.test");
        tool.ConfigureGovernance(risk, requiredRole: null, confirmationRequired: false, approvalRequired, auditRequired: true);
        return tool;
    }

    [Fact]
    public async Task An_unregistered_function_is_denied_and_never_runs()
    {
        using var harness = SignIn();

        var result = await InvokeAsync(harness, "Rogue", "delete_everything");

        Assert.False(harness.BodyRan);
        Assert.Contains("not a registered, governed tool", result);
    }

    [Fact]
    public async Task A_dynamic_tool_is_governed_by_its_id_even_when_its_name_contains_spaces()
    {
        using var harness = SignIn();
        var tool = RegisteredTool(harness.TenantId, "Get Supplier", risk: "High", approvalRequired: true);
        harness.Context.ToolDefinitions.Add(tool);
        await harness.Context.SaveChangesAsync();

        // The SK function name is the sanitised form; the registry name has a space.
        var result = await InvokeAsync(harness, "DynamicTools", "Get_Supplier", tool.Id);

        Assert.False(harness.BodyRan);
        Assert.Contains("requires administrator approval", result);
    }

    // A dynamic (Http, EndpointUrl-linked) tool's approval requirement now pauses for real, instead of
    // just denying: it creates a standalone ApprovalRequest carrying a durable DeferredToolCallPayload
    // — see AiFunctionAuditFilter's DenyApprovalRequired branch and ApprovalsController.Approve's
    // IDeferredToolCallExecutor wiring for the resume half.
    [Fact]
    public async Task A_dynamic_tools_approval_requirement_creates_a_real_replayable_approval_request()
    {
        using var harness = SignIn();
        var tool = RegisteredTool(harness.TenantId, "Get Supplier", risk: "High", approvalRequired: true);
        harness.Context.ToolDefinitions.Add(tool);
        await harness.Context.SaveChangesAsync();

        var result = await InvokeAsync(harness, "DynamicTools", "Get_Supplier", tool.Id);

        Assert.False(harness.BodyRan);
        Assert.Contains("approval request has been created", result);

        var created = await harness.Context.ApprovalRequests
            .SingleAsync(ar => ar.TenantId == harness.TenantId);
        Assert.Null(created.WorkflowId);
        Assert.Null(created.WorkflowInstanceId);
        var payload = R2WAI.Application.Common.Models.DeferredToolCallPayload.TryParse(created.Data);
        Assert.NotNull(payload);
        Assert.Equal(tool.Id, payload!.ToolDefinitionId);
    }

    // A built-in plugin method (e.g. WorkflowPlugin) has no standalone re-invocation path outside a
    // live Kernel — DynamicToolExecutor.IsExecutable only recognises Http tools — so its approval
    // requirement still denies outright, same message as before this change.
    [Fact]
    public async Task A_built_in_tools_approval_requirement_still_denies_outright_no_approval_request_created()
    {
        using var harness = SignIn();
        var tool = new ToolDefinition(Guid.NewGuid(), harness.TenantId, "start_workflow", ToolType.SemanticKernelFunction, "test");
        tool.ConfigureGovernance("High", requiredRole: null, confirmationRequired: false, approvalRequired: true, auditRequired: true);
        harness.Context.ToolDefinitions.Add(tool);
        await harness.Context.SaveChangesAsync();

        var result = await InvokeAsync(harness, "WorkflowPlugin", "start_workflow");

        Assert.False(harness.BodyRan);
        Assert.Equal("This action requires administrator approval and cannot be performed automatically yet.", result);
        Assert.False(await harness.Context.ApprovalRequests.AnyAsync(ar => ar.TenantId == harness.TenantId));
    }

    [Fact]
    public async Task A_registered_low_risk_dynamic_tool_still_runs()
    {
        using var harness = SignIn();
        var tool = RegisteredTool(harness.TenantId, "Get Supplier");
        harness.Context.ToolDefinitions.Add(tool);
        await harness.Context.SaveChangesAsync();

        var result = await InvokeAsync(harness, "DynamicTools", "Get_Supplier", tool.Id);

        Assert.True(harness.BodyRan);
        Assert.Equal("tool-ran", result);
    }

    [Fact]
    public async Task A_dynamic_tool_removed_after_the_kernel_was_built_is_denied()
    {
        using var harness = SignIn();

        var result = await InvokeAsync(harness, "DynamicTools", "Get_Supplier", toolDefinitionId: Guid.NewGuid());

        Assert.False(harness.BodyRan);
        Assert.Contains("not a registered, governed tool", result);
    }

    [Fact]
    public async Task A_caller_cannot_run_another_tenants_dynamic_tool_by_its_id()
    {
        var otherTenant = Guid.NewGuid();
        Guid toolId;
        using (var seed = SignIn(otherTenant))
        {
            var tool = RegisteredTool(otherTenant, "Get Supplier");
            seed.Context.ToolDefinitions.Add(tool);
            await seed.Context.SaveChangesAsync();
            toolId = tool.Id;
        }

        using var harness = SignIn(); // a different tenant
        var result = await InvokeAsync(harness, "DynamicTools", "Get_Supplier", toolId);

        Assert.False(harness.BodyRan);
        Assert.Contains("not a registered, governed tool", result);
    }

    [Fact]
    public async Task A_built_in_function_is_governed_by_code_defaults_for_a_tenant_with_no_rows()
    {
        using var harness = SignIn(); // a brand-new tenant: no seeded ToolDefinition rows

        var result = await InvokeAsync(harness, "WorkflowPlugin", "start_workflow");

        Assert.True(harness.BodyRan);
        Assert.Equal("tool-ran", result);
    }

    // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.6 #77 — the execution AuditLog row used to record only
    // {status, function, [error]}, dropping the timing the filter already measures via its own
    // Stopwatch. Deliberately not asserting on raw arguments here — those stay ephemeral
    // (IChatTraceCollector) rather than durably persisted, since they can contain whatever the AI
    // passed to any tool with no redaction; durationMs is a plain number, safe to persist as-is.
    [Fact]
    public async Task A_successful_tool_call_records_duration_on_its_audit_log_row()
    {
        using var harness = SignIn();
        var tool = RegisteredTool(harness.TenantId, "Get Supplier");
        harness.Context.ToolDefinitions.Add(tool);
        await harness.Context.SaveChangesAsync();

        await InvokeAsync(harness, "DynamicTools", "Get_Supplier", tool.Id);

        // Filtered to Execute specifically: creating the ToolDefinition above is itself separately
        // audited (a Create action against the same EntityId) — this test cares about the filter's
        // own execution-audit row, not that unrelated lifecycle entry.
        var auditLog = await harness.Context.AuditLogs
            .SingleAsync(a => a.TenantId == harness.TenantId && a.EntityId == tool.Id.ToString() && a.Action == AuditAction.Execute);
        var metadata = System.Text.Json.JsonDocument.Parse(auditLog.Metadata!).RootElement;
        Assert.Equal("executed", metadata.GetProperty("status").GetString());
        Assert.True(metadata.GetProperty("durationMs").GetInt64() >= 0);
    }

    [Fact]
    public async Task A_failed_tool_call_also_records_duration_alongside_the_error()
    {
        using var harness = SignIn();
        var tool = RegisteredTool(harness.TenantId, "Get Supplier");
        harness.Context.ToolDefinitions.Add(tool);
        await harness.Context.SaveChangesAsync();
        Func<string> throwing = () => throw new InvalidOperationException("boom");
        var throwingFunction = KernelFunctionFactory.CreateFromMethod(
            throwing,
            new KernelFunctionFromMethodOptions
            {
                FunctionName = "Get_Supplier",
                AdditionalMetadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                    new Dictionary<string, object?> { [AiFunctionAuditFilter.ToolDefinitionIdMetadataKey] = tool.Id }),
            });
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromFunctions("DynamicTools", [throwingFunction]);
        kernel.FunctionInvocationFilters.Add(harness.Scope.ServiceProvider.GetRequiredService<AiFunctionAuditFilter>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => kernel.InvokeAsync(kernel.Plugins.GetFunction("DynamicTools", "Get_Supplier")));

        var auditLog = await harness.Context.AuditLogs
            .SingleAsync(a => a.TenantId == harness.TenantId && a.EntityId == tool.Id.ToString() && a.Action == AuditAction.Execute);
        var metadata = System.Text.Json.JsonDocument.Parse(auditLog.Metadata!).RootElement;
        Assert.Equal("failed", metadata.GetProperty("status").GetString());
        Assert.Equal("boom", metadata.GetProperty("error").GetString());
        Assert.True(metadata.GetProperty("durationMs").GetInt64() >= 0);
    }

    [Fact]
    public async Task Semantic_kernel_utility_plugins_need_no_record()
    {
        using var harness = SignIn();

        var result = await InvokeAsync(harness, "TimePlugin", "Now");

        Assert.True(harness.BodyRan);
        Assert.Equal("tool-ran", result);
    }

    // ToolExecution ledger: every governed call leaves exactly one queryable row with its outcome.
    private static Task<List<ToolExecution>> LedgerAsync(Harness harness) =>
        harness.Context.ToolExecutions.Where(e => e.TenantId == harness.TenantId).ToListAsync();

    [Fact]
    public async Task Ledger_records_a_denied_call_with_its_reason()
    {
        using var harness = SignIn();

        await InvokeAsync(harness, "Rogue", "delete_everything");

        var execution = Assert.Single(await LedgerAsync(harness));
        Assert.Equal(ToolExecutionStatus.Denied, execution.Status);
        Assert.Equal("delete_everything", execution.Function);
        Assert.Equal("not a registered, governed tool", execution.DenialReason);
        Assert.Null(execution.ToolDefinitionId);
    }

    [Fact]
    public async Task Ledger_records_a_paused_call_as_awaiting_approval_linked_to_its_confirmation()
    {
        using var harness = SignIn();
        var tool = RegisteredTool(harness.TenantId, "Get Supplier", risk: "High", approvalRequired: true);
        harness.Context.ToolDefinitions.Add(tool);
        await harness.Context.SaveChangesAsync();

        await InvokeAsync(harness, "DynamicTools", "Get_Supplier", tool.Id);

        var approval = await harness.Context.ApprovalRequests.SingleAsync(ar => ar.TenantId == harness.TenantId);
        var execution = Assert.Single(await LedgerAsync(harness));
        Assert.Equal(ToolExecutionStatus.AwaitingApproval, execution.Status);
        Assert.Equal(approval.Id, execution.ApprovalRequestId);
        Assert.Equal(tool.Id, execution.ToolDefinitionId);
    }

    [Fact]
    public async Task Ledger_records_a_successful_call_with_its_tool_and_duration()
    {
        using var harness = SignIn();
        var tool = RegisteredTool(harness.TenantId, "Get Supplier");
        harness.Context.ToolDefinitions.Add(tool);
        await harness.Context.SaveChangesAsync();

        await InvokeAsync(harness, "DynamicTools", "Get_Supplier", tool.Id);

        var execution = Assert.Single(await LedgerAsync(harness));
        Assert.Equal(ToolExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(tool.Id, execution.ToolDefinitionId);
        Assert.NotNull(execution.DurationMs);
        Assert.NotNull(execution.CompletedAt);
    }

    [Fact]
    public async Task Ledger_never_links_a_built_in_default_that_has_no_database_row()
    {
        using var harness = SignIn(); // no ToolDefinition rows: start_workflow is governed by code defaults

        await InvokeAsync(harness, "WorkflowPlugin", "start_workflow");

        var execution = Assert.Single(await LedgerAsync(harness));
        Assert.Equal(ToolExecutionStatus.Succeeded, execution.Status);
        Assert.Null(execution.ToolDefinitionId); // a real FK in Postgres — the default's id is not a row
    }

    [Fact]
    public async Task Ledger_records_a_call_that_threw_as_failed_and_skips_utility_plugins()
    {
        using var harness = SignIn();
        var tool = RegisteredTool(harness.TenantId, "Get Supplier");
        harness.Context.ToolDefinitions.Add(tool);
        await harness.Context.SaveChangesAsync();
        Func<string> throwing = () => throw new InvalidOperationException("boom");
        var throwingFunction = KernelFunctionFactory.CreateFromMethod(
            throwing,
            new KernelFunctionFromMethodOptions
            {
                FunctionName = "Get_Supplier",
                AdditionalMetadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                    new Dictionary<string, object?> { [AiFunctionAuditFilter.ToolDefinitionIdMetadataKey] = tool.Id }),
            });
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromFunctions("DynamicTools", [throwingFunction]);
        kernel.FunctionInvocationFilters.Add(harness.Scope.ServiceProvider.GetRequiredService<AiFunctionAuditFilter>());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => kernel.InvokeAsync(kernel.Plugins.GetFunction("DynamicTools", "Get_Supplier")));

        await InvokeAsync(harness, "TimePlugin", "Now"); // ungoverned utility: not ledgered

        var execution = Assert.Single(await LedgerAsync(harness));
        Assert.Equal(ToolExecutionStatus.Failed, execution.Status);
        Assert.Equal("boom", execution.Error);
    }
}
