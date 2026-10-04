using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI.DynamicTools;

/// <summary>
/// The Microsoft Agent Framework counterpart to <see cref="DynamicToolFunctionFactory"/> — same
/// governed-<see cref="ToolDefinition"/>-to-callable-function role, targeting
/// <see cref="Microsoft.Extensions.AI.AIFunction"/> instead of a Semantic Kernel
/// <c>KernelFunction</c> (implementation plan Phase 4). Each function's own invocation delegate
/// calls <see cref="IToolGateway"/> directly — there is no MAF-side equivalent of
/// <c>AiFunctionAuditFilter</c> wired here; the governance call is inlined per function instead of
/// via a global interceptor, since MAF's tool set for this runtime is built fresh per call anyway.
///
/// Scoped to read-only HTTP tools only (GET/HEAD/OPTIONS, or no method recorded) — plan Phase 4's
/// explicit descope: "no write-capable tools on MAF yet." MCP tools are excluded outright: MCP has
/// no HTTP-verb concept to classify as read-only against, so exclude rather than guess.
/// </summary>
public class MafToolFunctionFactory
{
    private static readonly HashSet<string> ReadOnlyHttpMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "" };

    private readonly IRepository<ToolDefinition> _toolDefinitions;
    private readonly DynamicToolExecutor _executor;
    private readonly IToolGateway _toolGateway;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<MafToolFunctionFactory> _logger;

    public MafToolFunctionFactory(
        IRepository<ToolDefinition> toolDefinitions, DynamicToolExecutor executor, IToolGateway toolGateway,
        ICurrentUserService currentUser, ILogger<MafToolFunctionFactory> logger)
    {
        _toolDefinitions = toolDefinitions;
        _executor = executor;
        _toolGateway = toolGateway;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AITool>> BuildToolsAsync(IReadOnlyCollection<Guid>? enabledToolIds, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not { } tenantId)
            return [];

        var toolDefs = await _toolDefinitions.FindAsync(
            t => t.TenantId == tenantId && t.IsActive && t.ToolType == ToolType.Http
                && (t.ApplicationApiId != null || t.EndpointUrl != null)
                && (enabledToolIds == null || enabledToolIds.Contains(t.Id)),
            includePath: "ApplicationApi",
            ct);

        var tools = new List<AITool>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var toolDef in toolDefs.Where(t => ReadOnlyHttpMethods.Contains(t.HttpMethod ?? string.Empty)))
        {
            var functionName = DynamicToolFunctionFactory.SanitizeFunctionName(toolDef.Name);
            if (!usedNames.Add(functionName))
            {
                _logger.LogWarning(
                    "Skipping MAF tool '{ToolName}' — its sanitized function name '{FunctionName}' collides with another active read-only tool for this tenant",
                    toolDef.Name, functionName);
                continue;
            }

            var captured = toolDef;
            var description = string.IsNullOrWhiteSpace(captured.Description)
                ? $"Calls the '{captured.Name}' API."
                : captured.Description!;

            Func<string?, CancellationToken, Task<string>> invoke = async ([Description("Optional JSON request body for this API call.")] string? input, CancellationToken innerCt) =>
            {
                string? result = null;
                var outcome = await _toolGateway.InvokeAsync(new ToolInvocationRequest(
                    Plugin: "DynamicTools", Function: functionName, ToolDefinitionId: captured.Id,
                    ArgumentsForAudit: input, InputArgument: input,
                    ExecuteAsync: async gatewayCt => { result = await _executor.ExecuteAsync(captured, input, gatewayCt); }
                ), innerCt);
                return outcome.Allowed ? result ?? string.Empty : outcome.DenialMessage ?? "This action is not available.";
            };

            tools.Add(AIFunctionFactory.Create(invoke, functionName, description));
        }

        return tools;
    }
}
