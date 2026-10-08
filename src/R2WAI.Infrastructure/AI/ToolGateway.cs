using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using R2WAI.Application.Common;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.DynamicTools;
using R2WAI.Infrastructure.Services;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// The real implementation of the tool-call governance boundary — extracted from
/// <see cref="AiFunctionAuditFilter"/> (implementation plan Phase 2) so a future MCP or Agent
/// Framework adapter can reuse the exact same identity/tenant/role/risk/approval/audit pipeline
/// instead of re-implementing (or subtly diverging from) it. This is a behavior-preserving move,
/// not a redesign: every check, audit shape, and message below is unchanged from what
/// <c>AiFunctionAuditFilter.OnFunctionInvocationAsync</c> used to do inline. The pure decision
/// helpers (<see cref="AiFunctionAuditFilter.EvaluateGovernance"/>,
/// <see cref="AiFunctionAuditFilter.IsEnabledForCallingAssistant"/>,
/// <see cref="AiFunctionAuditFilter.HumanizeFunctionName"/>) deliberately stay on
/// <c>AiFunctionAuditFilter</c> rather than moving here — <c>IntegrationsController</c>'s "Test"
/// button and this class's own test suite already call them there directly, and moving them would
/// be a second, unrelated rename bundled into this extraction.
/// </summary>
public class ToolGateway : IToolGateway
{
    private readonly ILogger<ToolGateway> _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IChatTraceCollector _traceCollector;
    private readonly IRepository<ToolDefinition> _toolDefinitions;
    private readonly IToolExecutionPolicyService _policyService;
    private readonly IApprovalPolicyService _approvalPolicyService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IChatStreamContext _streamContext;
    private readonly IEnabledToolScope _enabledToolScope;

    public ToolGateway(
        ILogger<ToolGateway> logger,
        ICurrentUserService currentUser,
        IChatTraceCollector traceCollector,
        IRepository<ToolDefinition> toolDefinitions,
        IToolExecutionPolicyService policyService,
        IApprovalPolicyService approvalPolicyService,
        IServiceScopeFactory scopeFactory,
        IChatStreamContext streamContext,
        IEnabledToolScope enabledToolScope)
    {
        _logger = logger;
        _currentUser = currentUser;
        _traceCollector = traceCollector;
        _toolDefinitions = toolDefinitions;
        _policyService = policyService;
        _approvalPolicyService = approvalPolicyService;
        _scopeFactory = scopeFactory;
        _streamContext = streamContext;
        _enabledToolScope = enabledToolScope;
    }

    public async Task<ToolInvocationOutcome> InvokeAsync(ToolInvocationRequest request, CancellationToken ct = default)
    {
        var plugin = request.Plugin;
        var function = request.Function;

        _logger.LogInformation(
            "AI invoked function {Plugin}.{Function} with args {Arguments} (User {UserId}, Tenant {TenantId})",
            plugin, function, request.ArgumentsForAudit, _currentUser.UserId, _currentUser.TenantId);

        var isUtilityPlugin = BuiltInToolGovernance.IsUtilityPlugin(plugin);
        var (toolDef, isRegistered) = isUtilityPlugin ? (null, false) : await ResolveToolDefinitionAsync(request.ToolDefinitionId, function);
        // Only a ToolDefinition that exists in the database can be linked from the ledger — the
        // built-in defaults are code-defined records with fixed ids that are never persisted.
        var ledgerToolId = isRegistered ? toolDef?.Id : null;

        var decision = isUtilityPlugin
            ? GovernanceDecision.Allow
            : AiFunctionAuditFilter.EvaluateGovernance(toolDef, _currentUser.Roles);

        if (decision == GovernanceDecision.Allow && toolDef is not null && _currentUser.TenantId is { } policyTenantId)
        {
            var maxRiskLevel = await _policyService.GetMaxRiskLevelAsync(policyTenantId);
            if (ToolExecutionPolicyEvaluator.ExceedsCeiling(toolDef.RiskLevel, maxRiskLevel))
                decision = GovernanceDecision.DenyPolicyRiskCeiling;
        }

        if (decision == GovernanceDecision.Allow && toolDef is not null && _currentUser.TenantId is { } approvalTenantId)
        {
            var requireApprovalAbove = await _approvalPolicyService.GetRequireApprovalAboveRiskLevelAsync(approvalTenantId);
            if (ToolExecutionPolicyEvaluator.ExceedsCeiling(toolDef.RiskLevel, requireApprovalAbove))
                decision = GovernanceDecision.DenyApprovalRequired;
        }

        if (decision == GovernanceDecision.Allow && toolDef is not null
            && !AiFunctionAuditFilter.IsEnabledForCallingAssistant(toolDef.Id, _enabledToolScope.EnabledToolIds))
        {
            decision = GovernanceDecision.DenyNotEnabledForAssistant;
        }

        if (decision == GovernanceDecision.DenyUnknownTool)
        {
            _logger.LogWarning("Denied {Plugin}.{Function} — it has no governance record (unregistered tool)", plugin, function);
            await WriteUnknownToolDenialAuditAsync(function);
            await RecordDeniedAsync(null, plugin, function, "not a registered, governed tool");
            return new ToolInvocationOutcome(false, "This action is not available: it is not a registered, governed tool.");
        }

        if (decision == GovernanceDecision.DenyMissingRole)
        {
            _logger.LogWarning("Denied {Function} — user lacks required role {Role}", function, toolDef!.RequiredRole);
            await WriteDenialAuditAsync(toolDef, function, "missing required role");
            await RecordDeniedAsync(ledgerToolId, plugin, function, $"missing required role '{toolDef.RequiredRole}'");
            return new ToolInvocationOutcome(false,
                $"Access denied: this action requires the '{toolDef.RequiredRole}' role, which you do not have.");
        }

        if (decision == GovernanceDecision.DenyApprovalRequired)
        {
            if ((DynamicToolExecutor.IsExecutable(toolDef!) || McpDynamicToolExecutor.IsExecutable(toolDef!))
                && _currentUser.TenantId is { } tenantId && _currentUser.UserId is { } userId)
            {
                var requestId = await CreateDeferredApprovalRequestAsync(tenantId, userId, toolDef!, request.InputArgument);
                _logger.LogWarning("Paused {Function} for approval — created standalone approval request {RequestId}", function, requestId);
                await WriteDenialAuditAsync(toolDef!, function, "approval required — request created");
                if (ledgerToolId is { } pausedToolId)
                    await AddExecutionAsync(ToolExecution.AwaitingApproval(tenantId, userId, pausedToolId, plugin, function, requestId));
                return new ToolInvocationOutcome(false,
                    $"This action requires administrator approval. An approval request has been created (ID: {requestId}) " +
                    "and the assigned approver has been notified. It will run automatically once approved.");
            }

            _logger.LogWarning("Denied {Function} — approval required, not yet supported for this tool type", function);
            await WriteDenialAuditAsync(toolDef!, function, "approval required");
            await RecordDeniedAsync(ledgerToolId, plugin, function, "approval required, not yet supported for this tool type");
            return new ToolInvocationOutcome(false,
                "This action requires administrator approval and cannot be performed automatically yet.");
        }

        if (decision == GovernanceDecision.DenyPolicyRiskCeiling)
        {
            _logger.LogWarning("Denied {Function} — tool risk level {RiskLevel} exceeds this tenant's configured ToolExecution policy ceiling", function, toolDef!.RiskLevel);
            await WriteDenialAuditAsync(toolDef, function, "exceeds policy risk ceiling");
            await RecordDeniedAsync(ledgerToolId, plugin, function, "risk level exceeds the tenant's policy ceiling");
            return new ToolInvocationOutcome(false,
                "This action's risk level exceeds what this tenant's policy allows the AI to perform automatically.");
        }

        if (decision == GovernanceDecision.DenyNotEnabledForAssistant)
        {
            _logger.LogWarning("Denied {Function} — not in the calling assistant's enabled-tool list (defense-in-depth check)", function);
            await WriteDenialAuditAsync(toolDef!, function, "not enabled for this assistant");
            await RecordDeniedAsync(ledgerToolId, plugin, function, "not enabled for this assistant");
            return new ToolInvocationOutcome(false, "This action is not available to this assistant.");
        }

        var displayName = AiFunctionAuditFilter.HumanizeFunctionName(toolDef?.Name ?? function);
        if (_streamContext.OnProgress is { } onStarted)
            await onStarted(new ToolCallProgressEvent(displayName, ToolCallProgressKind.Started));

        // Prepare before Send (docs/architecture/EXECUTION-AND-WORKFLOWS.md): a row left in Prepared
        // means the process died mid-call, which is exactly what a later reconcile needs to find.
        // Utility plugins (no governance record) are not ledgered, matching how they skip governance.
        Guid? executionId = null;
        if (!isUtilityPlugin && _currentUser.TenantId is { } ledgerTenantId)
        {
            var prepared = ToolExecution.Prepared(ledgerTenantId, _currentUser.UserId, ledgerToolId, plugin, function);
            await AddExecutionAsync(prepared);
            executionId = prepared.Id;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await request.ExecuteAsync(ct);
            sw.Stop();
        }
        catch (OperationCanceledException ex)
        {
            // Cancelled or timed out after the call may already have reached the target.
            sw.Stop();
            await CompleteExecutionAsync(executionId, e => e.MarkUnknown(sw.ElapsedMilliseconds, ex.Message));
            RecordToolMetrics(plugin, function, sw.ElapsedMilliseconds, success: false);
            _traceCollector.RecordFunctionCall(plugin, function, request.ArgumentsForAudit, sw.ElapsedMilliseconds, success: false, error: ex.Message);
            if (toolDef?.AuditRequired ?? true)
                await WriteExecutionAuditAsync(toolDef, function, success: false, error: ex.Message, sw.ElapsedMilliseconds);
            if (_streamContext.OnProgress is { } onCancelled)
                await onCancelled(new ToolCallProgressEvent(displayName, ToolCallProgressKind.Completed, Success: false));
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            await CompleteExecutionAsync(executionId, e => e.Fail(sw.ElapsedMilliseconds, ex.Message));
            RecordToolMetrics(plugin, function, sw.ElapsedMilliseconds, success: false);
            _traceCollector.RecordFunctionCall(plugin, function, request.ArgumentsForAudit, sw.ElapsedMilliseconds, success: false, error: ex.Message);
            if (toolDef?.AuditRequired ?? true)
                await WriteExecutionAuditAsync(toolDef, function, success: false, error: ex.Message, sw.ElapsedMilliseconds);
            if (_streamContext.OnProgress is { } onFailed)
                await onFailed(new ToolCallProgressEvent(displayName, ToolCallProgressKind.Completed, Success: false));
            throw;
        }

        await CompleteExecutionAsync(executionId, e => e.Succeed(sw.ElapsedMilliseconds));
        RecordToolMetrics(plugin, function, sw.ElapsedMilliseconds, success: true);
        _traceCollector.RecordFunctionCall(plugin, function, request.ArgumentsForAudit, sw.ElapsedMilliseconds, success: true, error: null);
        if (toolDef?.AuditRequired ?? true)
            await WriteExecutionAuditAsync(toolDef, function, success: true, error: null, sw.ElapsedMilliseconds);
        if (_streamContext.OnProgress is { } onCompleted)
            await onCompleted(new ToolCallProgressEvent(displayName, ToolCallProgressKind.Completed, Success: true));

        _logger.LogInformation("AI function {Plugin}.{Function} completed", plugin, function);

        return new ToolInvocationOutcome(true, null);
    }

    private static void RecordToolMetrics(string plugin, string function, double elapsedMs, bool success)
    {
        DiagnosticsConfig.ToolInvocations.Add(1,
            new KeyValuePair<string, object?>("plugin", plugin),
            new KeyValuePair<string, object?>("function", function),
            new KeyValuePair<string, object?>("success", success));
        DiagnosticsConfig.ToolInvocationDuration.Record(elapsedMs,
            new KeyValuePair<string, object?>("plugin", plugin),
            new KeyValuePair<string, object?>("function", function));
    }

    // Caller-supplied id (e.g. Semantic Kernel function metadata) is tried first — exact,
    // collision-free, immune to a sanitised function name differing from ToolDefinition.Name.
    // Falls back to name-based lookup, then the code-defined built-in defaults.
    private async Task<(ToolDefinition? ToolDef, bool IsRegistered)> ResolveToolDefinitionAsync(Guid? toolDefinitionId, string function)
    {
        if (_currentUser.TenantId is not { } tenantId)
            return (null, false);

        if (toolDefinitionId is { } id)
        {
            var byId = await _toolDefinitions.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);
            return (byId, byId is not null);
        }

        var registered = await _toolDefinitions.FirstOrDefaultAsync(t => t.Name == function && t.TenantId == tenantId);
        return registered is not null ? (registered, true) : (BuiltInToolGovernance.TryCreateDefault(function, tenantId), false);
    }

    private async Task RecordDeniedAsync(Guid? toolDefinitionId, string plugin, string function, string reason)
    {
        if (_currentUser.TenantId is not { } tenantId) return;
        await AddExecutionAsync(ToolExecution.Denied(tenantId, _currentUser.UserId, toolDefinitionId, plugin, function, reason));
    }

    // Isolated scope for the same reason as WriteAuditAsync below.
    private async Task AddExecutionAsync(ToolExecution execution)
    {
        using var scope = _scopeFactory.CreateScope();
        var executions = scope.ServiceProvider.GetRequiredService<IRepository<ToolExecution>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await executions.AddAsync(execution);
        await unitOfWork.SaveChangesAsync();
    }

    private async Task CompleteExecutionAsync(Guid? executionId, Action<ToolExecution> complete)
    {
        if (executionId is not { } id) return;
        using var scope = _scopeFactory.CreateScope();
        var executions = scope.ServiceProvider.GetRequiredService<IRepository<ToolExecution>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var execution = await executions.GetByIdAsync(id);
        if (execution is null) return;
        complete(execution);
        executions.Update(execution);
        await unitOfWork.SaveChangesAsync();
    }

    private async Task WriteUnknownToolDenialAuditAsync(string function)
    {
        if (_currentUser.TenantId is not { } tenantId) return;
        var metadata = System.Text.Json.JsonSerializer.Serialize(
            new { status = "denied", reason = "no governance record", function });
        await WriteAuditAsync(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Execute, "ToolDefinition",
            function, _currentUser.UserId, metadata: metadata));
    }

    private async Task WriteDenialAuditAsync(ToolDefinition toolDef, string function, string reason)
    {
        if (!toolDef.AuditRequired || _currentUser.TenantId is not { } tenantId) return;
        var metadata = System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason, function });
        await WriteAuditAsync(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Execute, "ToolDefinition",
            toolDef.Id.ToString(), _currentUser.UserId, metadata: metadata));
    }

    private async Task WriteExecutionAuditAsync(ToolDefinition? toolDef, string function, bool success, string? error, long durationMs)
    {
        if (_currentUser.TenantId is not { } tenantId) return;
        var metadata = success
            ? System.Text.Json.JsonSerializer.Serialize(new { status = "executed", function, durationMs })
            : System.Text.Json.JsonSerializer.Serialize(new { status = "failed", function, error, durationMs });
        await WriteAuditAsync(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Execute, "ToolDefinition",
            toolDef?.Id.ToString() ?? function, _currentUser.UserId, metadata: metadata));
    }

    private async Task<Guid> CreateDeferredApprovalRequestAsync(Guid tenantId, Guid userId, ToolDefinition toolDef, string? input)
    {
        using var scope = _scopeFactory.CreateScope();
        var approvalService = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var payload = new DeferredToolCallPayload(toolDef.Id, input);
        var displayName = AiFunctionAuditFilter.HumanizeFunctionName(toolDef.Name);
        return await approvalService.CreateApprovalRequestAsync(
            tenantId, workflowInstanceId: null, workflowId: null, requesterId: userId,
            data: payload.ToJson(), subject: $"AI-initiated: {displayName}");
    }

    // Isolated scope/DbContext, not the ambient one — see AiFunctionAuditFilter's original doc
    // comment on this exact method (moved here verbatim): the caller's own chat request can hold a
    // long-lived DbContext across a multi-minute AI call, and sharing it for a fire-and-forget audit
    // write entangles this side effect with the caller's own unit of work.
    private async Task WriteAuditAsync(AuditLog auditLog)
    {
        using var scope = _scopeFactory.CreateScope();
        var auditLogs = scope.ServiceProvider.GetRequiredService<IRepository<AuditLog>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await auditLogs.AddAsync(auditLog);
        await unitOfWork.SaveChangesAsync();
    }
}
