using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI;

public enum GovernanceDecision
{
    Allow,
    DenyMissingRole,
    DenyApprovalRequired
}

/// <summary>
/// The gateway every autonomous Semantic Kernel tool call passes through. Looks up the governing
/// ToolDefinition ("Capability" — see ApplicationDbContextSeed's built-in tool rows) by function name
/// and enforces RequiredRole and ApprovalRequired by short-circuiting before the real call runs;
/// writes a real AuditLog row when AuditRequired (default true), closing the previous gap where tool
/// calls only ever reached application logs and the ephemeral chat trace collector, never the
/// queryable audit trail. Also still logs + records to IChatTraceCollector for Test Studio, as before.
///
/// ConfirmationRequired is intentionally NOT enforced here: autonomous function-calling has no
/// human-in-the-loop UI turn to pause on today. The field stays editable via the Capabilities UI for
/// when that hook exists; treat it as a documented gap, not an oversight.
/// </summary>
public class AiFunctionAuditFilter : IFunctionInvocationFilter
{
    private readonly ILogger<AiFunctionAuditFilter> _logger;
    private readonly ICurrentUserService _currentUser;
    private readonly IChatTraceCollector _traceCollector;
    private readonly IRepository<ToolDefinition> _toolDefinitions;
    private readonly IRepository<AuditLog> _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public AiFunctionAuditFilter(
        ILogger<AiFunctionAuditFilter> logger,
        ICurrentUserService currentUser,
        IChatTraceCollector traceCollector,
        IRepository<ToolDefinition> toolDefinitions,
        IRepository<AuditLog> auditLogs,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _currentUser = currentUser;
        _traceCollector = traceCollector;
        _toolDefinitions = toolDefinitions;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var plugin = context.Function.PluginName ?? "unknown";
        var function = context.Function.Name;
        var arguments = context.Arguments.Count > 0 ? string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}")) : null;

        _logger.LogInformation(
            "AI invoked function {Plugin}.{Function} with args {Arguments} (User {UserId}, Tenant {TenantId})",
            plugin, function, context.Arguments, _currentUser.UserId, _currentUser.TenantId);

        var toolDef = _currentUser.TenantId.HasValue
            ? await _toolDefinitions.FirstOrDefaultAsync(
                t => t.Name == function && t.ApplicationId == null && t.TenantId == _currentUser.TenantId.Value)
            : null;

        var decision = EvaluateGovernance(toolDef, _currentUser.Roles);

        if (decision == GovernanceDecision.DenyMissingRole)
        {
            _logger.LogWarning("Denied {Function} — user lacks required role {Role}", function, toolDef!.RequiredRole);
            await WriteDenialAuditAsync(toolDef!, function, "missing required role");
            context.Result = new FunctionResult(context.Function,
                $"Access denied: this action requires the '{toolDef!.RequiredRole}' role, which you do not have.");
            return;
        }

        if (decision == GovernanceDecision.DenyApprovalRequired)
        {
            _logger.LogWarning("Denied {Function} — approval required, not yet supported for direct AI invocation", function);
            await WriteDenialAuditAsync(toolDef!, function, "approval required");
            context.Result = new FunctionResult(context.Function,
                "This action requires administrator approval and cannot be performed automatically yet.");
            return;
        }

        var sw = Stopwatch.StartNew();
        var success = true;
        string? error = null;
        try
        {
            await next(context);
            sw.Stop();
        }
        catch (Exception ex)
        {
            sw.Stop();
            success = false;
            error = ex.Message;
            _traceCollector.RecordFunctionCall(plugin, function, arguments, sw.ElapsedMilliseconds, success: false, error: ex.Message);
            if (toolDef?.AuditRequired ?? true)
                await WriteExecutionAuditAsync(toolDef, function, success: false, error: ex.Message);
            throw;
        }

        _traceCollector.RecordFunctionCall(plugin, function, arguments, sw.ElapsedMilliseconds, success: true, error: null);
        if (toolDef?.AuditRequired ?? true)
            await WriteExecutionAuditAsync(toolDef, function, success: true, error: null);

        _logger.LogInformation(
            "AI function {Plugin}.{Function} completed", plugin, function);
    }

    /// <summary>
    /// Pure governance decision, isolated from Semantic Kernel types so it's directly unit-testable.
    /// Order matters: a missing required role denies before an approval requirement is even considered.
    /// </summary>
    public static GovernanceDecision EvaluateGovernance(ToolDefinition? toolDef, string[] userRoles)
    {
        if (toolDef is null) return GovernanceDecision.Allow;

        if (!string.IsNullOrEmpty(toolDef.RequiredRole)
            && !userRoles.Any(r => string.Equals(r, toolDef.RequiredRole, StringComparison.OrdinalIgnoreCase)))
            return GovernanceDecision.DenyMissingRole;

        if (toolDef.ApprovalRequired) return GovernanceDecision.DenyApprovalRequired;

        return GovernanceDecision.Allow;
    }

    private async Task WriteDenialAuditAsync(ToolDefinition toolDef, string function, string reason)
    {
        if (!toolDef.AuditRequired || _currentUser.TenantId is not { } tenantId) return;
        var metadata = System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason, function });
        await _auditLogs.AddAsync(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Execute, "ToolDefinition",
            toolDef.Id.ToString(), _currentUser.UserId, metadata: metadata));
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task WriteExecutionAuditAsync(ToolDefinition? toolDef, string function, bool success, string? error)
    {
        if (_currentUser.TenantId is not { } tenantId) return;
        var metadata = success
            ? System.Text.Json.JsonSerializer.Serialize(new { status = "executed", function })
            : System.Text.Json.JsonSerializer.Serialize(new { status = "failed", function, error });
        await _auditLogs.AddAsync(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Execute, "ToolDefinition",
            toolDef?.Id.ToString() ?? function, _currentUser.UserId, metadata: metadata));
        await _unitOfWork.SaveChangesAsync();
    }
}
