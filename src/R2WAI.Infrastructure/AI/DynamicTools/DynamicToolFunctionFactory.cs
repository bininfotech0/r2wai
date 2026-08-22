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
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<DynamicToolFunctionFactory> _logger;

    public DynamicToolFunctionFactory(
        IRepository<ToolDefinition> toolDefinitions,
        DynamicToolExecutor executor,
        ICurrentUserService currentUser,
        ILogger<DynamicToolFunctionFactory> logger)
    {
        _toolDefinitions = toolDefinitions;
        _executor = executor;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<KernelPlugin?> BuildPluginAsync(CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not { } tenantId)
            return null;

        // Eligible either via an ApplicationApi link (the governed-capability shape) or a directly
        // configured EndpointUrl (the shape the Integrations UI actually creates — "Add Integration"
        // sets EndpointUrl straight on the ToolDefinition, never ApplicationApiId).
        var toolDefs = await _toolDefinitions.FindAsync(
            t => t.TenantId == tenantId && t.IsActive && t.ToolType == ToolType.Http
                && (t.ApplicationApiId != null || t.EndpointUrl != null),
            includePath: "ApplicationApi",
            ct);

        if (toolDefs.Count == 0)
            return null;

        var functions = new List<KernelFunction>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var toolDef in toolDefs)
        {
            var functionName = SanitizeFunctionName(toolDef.Name);

            if (!usedNames.Add(functionName))
            {
                _logger.LogWarning(
                    "Skipping dynamic tool '{ToolName}' — its sanitized function name '{FunctionName}' collides with another active tool for this tenant",
                    toolDef.Name, functionName);
                continue;
            }

            var captured = toolDef;
            var description = string.IsNullOrWhiteSpace(captured.Description)
                ? $"Calls the '{captured.Name}' API."
                : captured.Description!;

            var function = KernelFunctionFactory.CreateFromMethod(
                method: ([Description("Optional JSON request body for this API call.")] string? input, CancellationToken innerCt) =>
                    _executor.ExecuteAsync(captured, input, innerCt),
                functionName: functionName,
                description: description);

            functions.Add(function);
        }

        return functions.Count > 0
            ? KernelPluginFactory.CreateFromFunctions("DynamicTools", "Application-registered API tools", functions)
            : null;
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
