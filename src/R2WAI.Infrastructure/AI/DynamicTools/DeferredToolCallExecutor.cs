using Microsoft.Extensions.Logging;
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
    private readonly ILogger<DeferredToolCallExecutor> _logger;

    public DeferredToolCallExecutor(
        IRepository<ToolDefinition> toolDefinitions, DynamicToolExecutor executor,
        McpDynamicToolExecutor mcpExecutor, ILogger<DeferredToolCallExecutor> logger)
    {
        _toolDefinitions = toolDefinitions;
        _executor = executor;
        _mcpExecutor = mcpExecutor;
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
            return "The tool this approval was for no longer exists.";
        }

        // The Include's target navigation depends on ToolType, and GenericRepository's string-path
        // Include only supports one navigation per query — so re-fetch with the right one now that
        // ToolType is known, mirroring DynamicToolFunctionFactory.BuildPluginAsync's own split.
        string result;
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

        _logger.LogInformation(
            "Approval {RequestId} approved — executed deferred tool call '{ToolName}'", request.Id, toolDef.Name);
        return result;
    }
}
