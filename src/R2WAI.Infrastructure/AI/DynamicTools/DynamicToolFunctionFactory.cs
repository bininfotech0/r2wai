using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI.DynamicTools;

/// <summary>
/// Turns a tenant's admin-registered <see cref="ToolDefinition"/> rows (Integrations UI, backed by
/// <see cref="ApplicationApi"/>) into real Semantic Kernel functions, so registering an integration
/// actually makes it agent-callable instead of only existing as a governance/audit record checked by
/// name. Scoped to ToolType.Http for now — Database/Script/Custom tool types stay
/// governance-record-only until they have a safe, well-defined execution path.
/// </summary>
public class DynamicToolFunctionFactory
{
    private readonly IRepository<ToolDefinition> _toolDefinitions;
    private readonly DynamicToolExecutor _executor;
    private readonly McpDynamicToolExecutor _mcpExecutor;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<DynamicToolFunctionFactory> _logger;

    public DynamicToolFunctionFactory(
        IRepository<ToolDefinition> toolDefinitions,
        DynamicToolExecutor executor,
        McpDynamicToolExecutor mcpExecutor,
        ICurrentUserService currentUser,
        ILogger<DynamicToolFunctionFactory> logger)
    {
        _toolDefinitions = toolDefinitions;
        _executor = executor;
        _mcpExecutor = mcpExecutor;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<KernelPlugin?> BuildPluginAsync(IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not { } tenantId)
            return null;

        // Eligible either via an ApplicationApi link (the governed-capability shape) or a directly
        // configured EndpointUrl (the shape the Integrations UI actually creates — "Add Integration"
        // sets EndpointUrl straight on the ToolDefinition, never ApplicationApiId). enabledToolIds
        // null means "not configured" (every eligible tool, unchanged from before this filtering
        // existed) — see SemanticKernelService.GetOrCreateKernelAsync for the full rationale.
        var httpToolDefs = await _toolDefinitions.FindAsync(
            t => t.TenantId == tenantId && t.IsActive && t.ToolType == ToolType.Http
                && (t.ApplicationApiId != null || t.EndpointUrl != null)
                && (enabledToolIds == null || enabledToolIds.Contains(t.Id)),
            includePath: "ApplicationApi",
            ct);

        // Separate query, not a second Include on the same call — GenericRepository.FindAsync's
        // string-path Include only supports one navigation per call, and Http/Mcp tools are
        // mutually exclusive by ToolType anyway, so two narrow queries are simpler than widening
        // the repository abstraction for a single caller.
        var mcpToolDefs = await _toolDefinitions.FindAsync(
            t => t.TenantId == tenantId && t.IsActive && t.ToolType == ToolType.Mcp
                && t.McpServerConnectionId != null
                && (enabledToolIds == null || enabledToolIds.Contains(t.Id)),
            includePath: "McpServerConnection",
            ct);

        if (httpToolDefs.Count == 0 && mcpToolDefs.Count == 0)
            return null;

        var functions = new List<KernelFunction>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var toolDef in httpToolDefs)
            AddFunctionIfNameFree(functions, usedNames, toolDef,
                $"Calls the '{toolDef.Name}' API.",
                (input, innerCt) => _executor.ExecuteAsync(toolDef, input, innerCt));

        foreach (var toolDef in mcpToolDefs)
            AddFunctionIfNameFree(functions, usedNames, toolDef,
                $"Calls the '{toolDef.McpToolName}' tool on the '{toolDef.McpServerConnection!.Name}' MCP server.",
                (input, innerCt) => _mcpExecutor.ExecuteAsync(toolDef, input, innerCt));

        return functions.Count > 0
            ? KernelPluginFactory.CreateFromFunctions("DynamicTools", "Application-registered API and MCP tools", functions)
            : null;
    }

    private void AddFunctionIfNameFree(
        List<KernelFunction> functions, HashSet<string> usedNames, ToolDefinition toolDef,
        string defaultDescription, Func<string?, CancellationToken, Task<string>> execute)
    {
        var functionName = SanitizeFunctionName(toolDef.Name);

        if (!usedNames.Add(functionName))
        {
            _logger.LogWarning(
                "Skipping dynamic tool '{ToolName}' — its sanitized function name '{FunctionName}' collides with another active tool for this tenant",
                toolDef.Name, functionName);
            return;
        }

        var description = string.IsNullOrWhiteSpace(toolDef.Description) ? defaultDescription : toolDef.Description!;

        // The registry id travels with the function so AiFunctionAuditFilter can find the governing
        // ToolDefinition exactly. It used to look the tool up by function name, which never matched
        // when the admin's free-text tool name differed from the sanitised function name — leaving
        // that tool without governance.
        var function = KernelFunctionFactory.CreateFromMethod(
            method: ([Description("Optional JSON matching this tool's input schema.")] string? input, CancellationToken innerCt) =>
                execute(input, innerCt),
            options: new KernelFunctionFromMethodOptions
            {
                FunctionName = functionName,
                Description = description,
                AdditionalMetadata = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                    new Dictionary<string, object?>
                    {
                        [AiFunctionAuditFilter.ToolDefinitionIdMetadataKey] = toolDef.Id
                    })
            });

        functions.Add(function);
    }

    // Semantic Kernel function names must be letters/digits/underscore only; ToolDefinition.Name is
    // free text (admin-entered in the Integrations/Tools UI), so this maps arbitrary names into that
    // space deterministically rather than rejecting anything with spaces or punctuation.
    private static readonly Regex InvalidNameChars = new(@"[^A-Za-z0-9_]", RegexOptions.Compiled);

    public static string SanitizeFunctionName(string name)
    {
        var sanitized = InvalidNameChars.Replace(name.Trim(), "_");
        if (sanitized.Length == 0 || char.IsDigit(sanitized[0]))
            sanitized = "tool_" + sanitized;
        return sanitized.Length > 64 ? sanitized[..64] : sanitized;
    }
}
