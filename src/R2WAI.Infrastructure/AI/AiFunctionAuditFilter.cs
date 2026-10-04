using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;

namespace R2WAI.Infrastructure.AI;

public enum GovernanceDecision
{
    Allow,
    DenyMissingRole,
    DenyApprovalRequired,
    DenyPolicyRiskCeiling,
    DenyNotEnabledForAssistant,

    /// <summary>The function has no governance record at all (not registered, not a known built-in): fail closed.</summary>
    DenyUnknownTool
}

/// <summary>
/// The Semantic Kernel adapter for <see cref="IToolGateway"/> (implementation plan Phase 2) — every
/// autonomous SK tool call passes through here, but the actual governance/audit orchestration now
/// lives in <see cref="ToolGateway"/>, runtime-agnostic, so a future MCP or Agent Framework adapter
/// can reuse it instead of re-implementing (or diverging from) the same checks. This class's only
/// remaining job is translating between Semantic Kernel's <see cref="FunctionInvocationContext"/>
/// and <see cref="ToolInvocationRequest"/>/<see cref="ToolInvocationOutcome"/>.
///
/// The pure decision helpers below (<see cref="EvaluateGovernance"/>,
/// <see cref="IsEnabledForCallingAssistant"/>, <see cref="HumanizeFunctionName"/>) and
/// <see cref="ToolDefinitionIdMetadataKey"/> stay here rather than moving to <see cref="ToolGateway"/>:
/// <c>IntegrationsController</c>'s "Test" button and <c>DynamicToolFunctionFactory</c>'s SK function
/// metadata stamping already reference them at this exact location, and this extraction is meant to
/// be behavior-preserving, not a second, unrelated rename.
/// </summary>
public class AiFunctionAuditFilter : IFunctionInvocationFilter
{
    /// <summary>
    /// Key under which a dynamic (tenant-registered) tool's function carries its ToolDefinition id, so the
    /// filter can look the governing record up exactly instead of guessing from the function name.
    /// </summary>
    public const string ToolDefinitionIdMetadataKey = "r2wai.toolDefinitionId";

    private readonly IToolGateway _toolGateway;

    public AiFunctionAuditFilter(IToolGateway toolGateway)
    {
        _toolGateway = toolGateway;
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var plugin = context.Function.PluginName ?? "unknown";
        var function = context.Function.Name;
        var argumentsForAudit = context.Arguments.Count > 0
            ? string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}"))
            : null;
        var inputArgument = context.Arguments.TryGetValue("input", out var rawInput) ? rawInput as string : null;

        Guid? toolDefinitionId = context.Function.Metadata.AdditionalProperties.TryGetValue(ToolDefinitionIdMetadataKey, out var raw)
            && raw is Guid id
            ? id
            : null;

        var request = new ToolInvocationRequest(
            plugin, function, toolDefinitionId, argumentsForAudit, inputArgument,
            ExecuteAsync: _ => next(context));

        var outcome = await _toolGateway.InvokeAsync(request);

        if (!outcome.Allowed)
            context.Result = new FunctionResult(context.Function, outcome.DenialMessage);
    }

    /// <summary>
    /// Defense-in-depth check (see call site) — pure so it's directly unit-testable without a live
    /// Semantic Kernel context. Null enabledToolIds means "not configured", matching
    /// SemanticKernelService.GetOrCreateKernelAsync's own semantics exactly: unfiltered/allow, not
    /// deny-all — an assistant with no explicit tool selection keeps working exactly as before.
    /// </summary>
    public static bool IsEnabledForCallingAssistant(Guid toolDefId, IReadOnlyCollection<Guid>? enabledToolIds)
        => enabledToolIds is null || enabledToolIds.Contains(toolDefId);

    /// <summary>
    /// Turns a technical function/tool name (snake_case, PascalCase, or a mix — e.g.
    /// "get_leave_balance" or "SubmitInvoice") into a human-readable display name ("Get Leave Balance",
    /// "Submit Invoice") for the in-chat progress UX. Never shows the raw technical name to the user.
    /// </summary>
    public static string HumanizeFunctionName(string name)
    {
        var spaced = Regex.Replace(name, "(?<!^)([A-Z])", " $1").Replace('_', ' ').Replace('-', ' ');
        var words = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : string.Empty));
        return string.Join(' ', words);
    }

    /// <summary>
    /// Pure governance decision, isolated from Semantic Kernel types so it's directly unit-testable.
    /// Order matters: a missing required role denies before an approval requirement is even considered.
    /// </summary>
    public static GovernanceDecision EvaluateGovernance(ToolDefinition? toolDef, string[] userRoles)
    {
        if (toolDef is null) return GovernanceDecision.DenyUnknownTool;

        if (!string.IsNullOrEmpty(toolDef.RequiredRole)
            && !userRoles.Any(r => string.Equals(r, toolDef.RequiredRole, StringComparison.OrdinalIgnoreCase)))
            return GovernanceDecision.DenyMissingRole;

        if (toolDef.ApprovalRequired) return GovernanceDecision.DenyApprovalRequired;

        return GovernanceDecision.Allow;
    }
}
