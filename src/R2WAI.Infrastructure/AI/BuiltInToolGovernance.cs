using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;

namespace R2WAI.Infrastructure.AI;

/// <summary>
/// Governance defaults for the platform's own tool functions (the [KernelFunction]s on WorkflowPlugin,
/// DocumentPlugin, RAGPlugin and AssistantPlugin).
///
/// Their governing ToolDefinition rows are seeded for the default tenant only, and the governance
/// filter used to ALLOW any function that had no row — so for any other tenant the mutating built-ins
/// (start_workflow, submit_approval_request, notify_approver) ran with none of RequiredRole, approval,
/// risk ceiling or approval-above-risk applied. A missing row now falls back to these defaults, which
/// are the same ids and risk levels the seed uses (so an assistant's stored enabled-tool ids mean the
/// same thing here), and a function that is neither registered nor listed here is denied.
/// </summary>
public static class BuiltInToolGovernance
{
    // Semantic Kernel utility plugins attached to every kernel (SemanticKernelService.CreateKernel):
    // pure helpers with no side effects on tenant data, so they are not governed individually.
    private static readonly HashSet<string> UtilityPlugins = new(StringComparer.Ordinal)
    {
        "ConversationSummaryPlugin",
        "TimePlugin",
    };

    private sealed record Entry(Guid Id, string RiskLevel, string Description);

    // Keep in step with ApplicationDbContextSeed's built-in tool rows.
    private static readonly Dictionary<string, Entry> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["start_workflow"] = new(Guid.Parse("00000000-0000-0000-0000-000000000401"), "Medium", "Start (run) a workflow by name or ID"),
        ["submit_approval_request"] = new(Guid.Parse("00000000-0000-0000-0000-000000000402"), "Medium", "Submit a new approval request for a workflow instance"),
        ["get_workflow_status"] = new(Guid.Parse("00000000-0000-0000-0000-000000000403"), "Low", "Get the current status of a workflow instance"),
        ["notify_approver"] = new(Guid.Parse("00000000-0000-0000-0000-000000000404"), "Medium", "Send a notification to a workflow approver"),
        ["list_pending_approvals"] = new(Guid.Parse("00000000-0000-0000-0000-000000000405"), "Low", "List pending approval requests for the current user"),
        ["search_knowledge_base"] = new(Guid.Parse("00000000-0000-0000-0000-000000000406"), "Low", "Search a knowledge base using semantic search"),
        ["retrieve_documents"] = new(Guid.Parse("00000000-0000-0000-0000-000000000407"), "Low", "Retrieve documents from a knowledge base"),
        ["get_citations"] = new(Guid.Parse("00000000-0000-0000-0000-000000000408"), "Low", "Get citations from search results"),
        ["summarize_document"] = new(Guid.Parse("00000000-0000-0000-0000-000000000409"), "Low", "Summarize a document by its ID"),
        ["extract_from_document"] = new(Guid.Parse("00000000-0000-0000-0000-000000000410"), "Low", "Extract structured data from a document using a schema"),
        ["compare_documents"] = new(Guid.Parse("00000000-0000-0000-0000-000000000411"), "Low", "Compare two documents and return the comparison result"),
        ["ask_document"] = new(Guid.Parse("00000000-0000-0000-0000-000000000412"), "Low", "Ask a question about a document"),
        ["get_assistant_context"] = new(Guid.Parse("00000000-0000-0000-0000-000000000413"), "Low", "Get the context and configuration for an assistant"),
        ["get_knowledge_base_context"] = new(Guid.Parse("00000000-0000-0000-0000-000000000414"), "Low", "Get context from a knowledge base for answering questions"),
    };

    public static IReadOnlyCollection<string> KnownFunctionNames => Defaults.Keys;

    public static bool IsUtilityPlugin(string? pluginName) =>
        pluginName is not null && UtilityPlugins.Contains(pluginName);

    /// <summary>A governance record for a built-in function (not persisted by this call), or null if the name isn't one.</summary>
    public static ToolDefinition? TryCreateDefault(string functionName, Guid tenantId)
    {
        if (!Defaults.TryGetValue(functionName, out var entry))
            return null;

        var tool = new ToolDefinition(entry.Id, tenantId, functionName, ToolType.SemanticKernelFunction, entry.Description);
        tool.ConfigureGovernance(entry.RiskLevel, requiredRole: null, confirmationRequired: false,
            approvalRequired: false, auditRequired: true);
        return tool;
    }
}
