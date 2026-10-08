using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using R2WAI.Application.Common;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI.DynamicTools;

public class DeferredToolCallExecutor : IDeferredToolCallExecutor
{
    private readonly IRepository<ToolDefinition> _toolDefinitions;
    private readonly DynamicToolExecutor _executor;
    private readonly McpDynamicToolExecutor _mcpExecutor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeferredToolCallExecutor> _logger;

    public DeferredToolCallExecutor(
        IRepository<ToolDefinition> toolDefinitions, DynamicToolExecutor executor,
        McpDynamicToolExecutor mcpExecutor, IServiceScopeFactory scopeFactory, ILogger<DeferredToolCallExecutor> logger)
    {
        _toolDefinitions = toolDefinitions;
        _executor = executor;
        _mcpExecutor = mcpExecutor;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<string?> TryExecuteAsync(ApprovalRequest request, CancellationToken ct = default)
    {
        var payload = DeferredToolCallPayload.TryParse(request.Data);
        if (payload is null)
            return null;

        // Tenant-scoped lookup, same guard AiFunctionAuditFilter.ResolveToolDefinitionAsync applies at
        // capture time — a request cannot be replayed against a different tenant's tool even if its id
        // were somehow reused.
        var toolDef = await _toolDefinitions.FirstOrDefaultAsync(
            t => t.Id == payload.ToolDefinitionId && t.TenantId == request.TenantId, ct);

        if (toolDef is null)
        {
            _logger.LogWarning(
                "Approval {RequestId} was approved but its deferred tool {ToolDefinitionId} no longer exists for tenant {TenantId}",
                request.Id, payload.ToolDefinitionId, request.TenantId);
            await UpdateLedgerAsync(request, e => e.RejectApproval("the tool no longer exists"));
            return "The tool this approval was for no longer exists.";
        }

        var executionId = await PrepareLedgerAsync(request, toolDef);

        // The Include's target navigation depends on ToolType, and GenericRepository's string-path
        // Include only supports one navigation per query — so re-fetch with the right one now that
        // ToolType is known, mirroring DynamicToolFunctionFactory.BuildPluginAsync's own split.
        // An approved call used to run outside ToolGateway entirely: no audit row, no metrics, and an
        // exception was only logged by the controller — so the riskiest actions, the ones that needed
        // approval, were the ones with no execution record. Record it the way the gateway does.
        var sw = Stopwatch.StartNew();
        string result;
        try
        {
            if (toolDef.ToolType == ToolType.Mcp)
            {
                var mcpToolDef = await _toolDefinitions.FindAsync(
                    t => t.Id == toolDef.Id, includePath: "McpServerConnection", ct);
                result = await _mcpExecutor.ExecuteAsync(mcpToolDef[0], payload.Input, ct);
            }
            else
            {
                var httpToolDef = await _toolDefinitions.FindAsync(
                    t => t.Id == toolDef.Id, includePath: "ApplicationApi", ct);
                result = await _executor.ExecuteAsync(httpToolDef[0], payload.Input, ct);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogError(ex, "Approval {RequestId} approved but deferred tool call '{ToolName}' failed", request.Id, toolDef.Name);
            RecordMetrics(toolDef, sw.ElapsedMilliseconds, success: false);
            await CompleteLedgerAsync(executionId, e => e.Fail(sw.ElapsedMilliseconds, ex.Message));
            await WriteExecutionAuditAsync(request, toolDef, success: false, ex.Message, sw.ElapsedMilliseconds);
            return $"Execution failed: {ex.Message}";
        }

        sw.Stop();
        RecordMetrics(toolDef, sw.ElapsedMilliseconds, success: true);
        await CompleteLedgerAsync(executionId, e => e.Succeed(sw.ElapsedMilliseconds));
        await WriteExecutionAuditAsync(request, toolDef, success: true, error: null, sw.ElapsedMilliseconds);
        _logger.LogInformation(
            "Approval {RequestId} approved — executed deferred tool call '{ToolName}'", request.Id, toolDef.Name);
        return result;
    }

    public async Task RecordRejectionAsync(ApprovalRequest request, CancellationToken ct = default)
    {
        if (DeferredToolCallPayload.TryParse(request.Data) is null)
            return;
        await UpdateLedgerAsync(request, e => e.RejectApproval("confirmation rejected"));
    }

    // The gateway wrote an AwaitingApproval row when it paused this call; move it to Prepared. A
    // request paused before the ledger existed has no row, so one is created for it here.
    private async Task<Guid> PrepareLedgerAsync(ApprovalRequest request, ToolDefinition toolDef)
    {
        using var scope = _scopeFactory.CreateScope();
        var executions = scope.ServiceProvider.GetRequiredService<IRepository<ToolExecution>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var execution = await executions.FirstOrDefaultAsync(
            e => e.ApprovalRequestId == request.Id && e.Status == ToolExecutionStatus.AwaitingApproval);
        if (execution is null)
        {
            execution = ToolExecution.Prepared(request.TenantId, request.RequesterId, toolDef.Id, "Approved", toolDef.Name, request.Id);
            await executions.AddAsync(execution);
        }
        else
        {
            execution.PrepareAfterApproval();
            executions.Update(execution);
        }
        await unitOfWork.SaveChangesAsync();
        return execution.Id;
    }

    private async Task CompleteLedgerAsync(Guid executionId, Action<ToolExecution> complete)
    {
        using var scope = _scopeFactory.CreateScope();
        var executions = scope.ServiceProvider.GetRequiredService<IRepository<ToolExecution>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var execution = await executions.GetByIdAsync(executionId);
        if (execution is null) return;
        complete(execution);
        executions.Update(execution);
        await unitOfWork.SaveChangesAsync();
    }

    private async Task UpdateLedgerAsync(ApprovalRequest request, Action<ToolExecution> update)
    {
        using var scope = _scopeFactory.CreateScope();
        var executions = scope.ServiceProvider.GetRequiredService<IRepository<ToolExecution>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var execution = await executions.FirstOrDefaultAsync(
            e => e.ApprovalRequestId == request.Id && e.Status == ToolExecutionStatus.AwaitingApproval);
        if (execution is null) return;
        update(execution);
        executions.Update(execution);
        await unitOfWork.SaveChangesAsync();
    }

    private static void RecordMetrics(ToolDefinition toolDef, double elapsedMs, bool success)
    {
        DiagnosticsConfig.ToolInvocations.Add(1,
            new KeyValuePair<string, object?>("plugin", "Approved"),
            new KeyValuePair<string, object?>("function", toolDef.Name),
            new KeyValuePair<string, object?>("success", success));
        DiagnosticsConfig.ToolInvocationDuration.Record(elapsedMs,
            new KeyValuePair<string, object?>("plugin", "Approved"),
            new KeyValuePair<string, object?>("function", toolDef.Name));
    }

    // Same AuditLog shape ToolGateway writes for a direct call, plus the approval it ran under. The
    // actor is the requester (the call is theirs); the approver is recorded alongside. Isolated scope
    // for the same reason ToolGateway.WriteAuditAsync gives: don't entangle this write with the
    // caller's own unit of work.
    private async Task WriteExecutionAuditAsync(ApprovalRequest request, ToolDefinition toolDef, bool success, string? error, long durationMs)
    {
        if (!toolDef.AuditRequired)
            return;

        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = success ? "executed" : "failed",
            function = toolDef.Name,
            error,
            durationMs,
            approvalRequestId = request.Id,
            approvedBy = request.ApproverId,
        });

        using var scope = _scopeFactory.CreateScope();
        var auditLogs = scope.ServiceProvider.GetRequiredService<IRepository<AuditLog>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await auditLogs.AddAsync(new AuditLog(Guid.NewGuid(), request.TenantId, AuditAction.Execute, "ToolDefinition",
            toolDef.Id.ToString(), request.RequesterId, metadata: metadata));
        await unitOfWork.SaveChangesAsync();
    }
}
