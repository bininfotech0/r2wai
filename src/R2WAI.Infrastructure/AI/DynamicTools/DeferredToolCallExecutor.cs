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

        var prepared = await PrepareLedgerAsync(request, toolDef);
        if (prepared.ShortCircuitMessage is not null)
            return prepared.ShortCircuitMessage;
        var executionId = prepared.ExecutionId;

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
            // ex.Message stays in the log, ledger row and audit entry; this text lands on the approval
            // comments the requester and approver read, so it carries no upstream detail.
            return "Execution failed. The error was recorded for administrators in the activity log.";
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
    //
    // Looked up by ApprovalRequestId regardless of status (not just AwaitingApproval) so a second
    // call for the same approval — there's no retry path that reaches this today (ApproveAsync's
    // atomic Pending/Escalated->Approved claim means the one known caller can't re-enter for an
    // already-decided request), but TryExecuteAsync is public on an interface, and the ledger's own
    // IdempotencyKey column exists specifically so a future caller (a reconcile sweep, a manual
    // retry) doesn't have to re-derive this safety check — never re-executes the external call.
    private async Task<LedgerPrepareResult> PrepareLedgerAsync(ApprovalRequest request, ToolDefinition toolDef)
    {
        using var scope = _scopeFactory.CreateScope();
        var executions = scope.ServiceProvider.GetRequiredService<IRepository<ToolExecution>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var execution = await executions.FirstOrDefaultAsync(e => e.ApprovalRequestId == request.Id);

        // Exhaustive over every ToolExecutionStatus a found row could be in, not just the two this
        // was first written for — Denied/Unknown would otherwise fall into the "proceed" branch
        // below and throw out of PrepareAfterApproval's EnsureStatus guard instead of denying
        // gracefully (caught by the one current caller today, but not something to rely on).
        switch (execution?.Status)
        {
            case ToolExecutionStatus.Succeeded:
            case ToolExecutionStatus.Failed:
            case ToolExecutionStatus.Denied:
                _logger.LogInformation(
                    "Approval {RequestId} was already resumed (ledger status {Status}) — not re-executing", request.Id, execution!.Status);
                return new LedgerPrepareResult(Guid.Empty, $"This approval was already decided (result: {execution.Status}).");

            case ToolExecutionStatus.Prepared:
            case ToolExecutionStatus.Unknown:
                _logger.LogWarning(
                    "Approval {RequestId}'s prior execution attempt never reached a terminal state — refusing to blindly retry", request.Id);
                return new LedgerPrepareResult(Guid.Empty,
                    "A previous execution attempt for this approval did not complete cleanly. Manual verification is required before retrying.");

            case ToolExecutionStatus.AwaitingApproval:
                execution!.PrepareAfterApproval();
                executions.Update(execution);
                break;

            case null:
                execution = ToolExecution.Prepared(request.TenantId, request.RequesterId, toolDef.Id, "Approved", toolDef.Name, request.Id);
                await executions.AddAsync(execution);
                break;
        }

        await unitOfWork.SaveChangesAsync();
        return new LedgerPrepareResult(execution!.Id, ShortCircuitMessage: null);
    }

    /// <summary><paramref name="ShortCircuitMessage"/> set means the caller must return it as-is and
    /// never invoke the tool — <paramref name="ExecutionId"/> is meaningless (<see cref="Guid.Empty"/>)
    /// in that case.</summary>
    private sealed record LedgerPrepareResult(Guid ExecutionId, string? ShortCircuitMessage);

    // The approved call already ran, so a ledger failure must not stop its result and audit row from
    // being recorded — same rule as ToolGateway.CompleteExecutionAsync. The row stays Prepared.
    private async Task CompleteLedgerAsync(Guid executionId, Action<ToolExecution> complete)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record the outcome of approved tool execution {ExecutionId} in the ledger", executionId);
        }
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

        // Written after the approved call ran; a failure must not hide its result from the approval
        // (same rule as ToolGateway.WriteAuditAsync). The metadata goes to the log instead.
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var auditLogs = scope.ServiceProvider.GetRequiredService<IRepository<AuditLog>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await auditLogs.AddAsync(new AuditLog(Guid.NewGuid(), request.TenantId, AuditAction.Execute, "ToolDefinition",
                toolDef.Id.ToString(), request.RequesterId, metadata: metadata));
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for approved tool call {ToolDefinitionId}: {Metadata}", toolDef.Id, metadata);
        }
    }
}
