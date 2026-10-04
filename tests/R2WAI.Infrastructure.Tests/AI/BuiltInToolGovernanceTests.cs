using System.Reflection;
using Microsoft.SemanticKernel;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.AI.Plugins;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Built-in tool functions have ToolDefinition rows for the seeded default tenant only. A function with no
/// governance record is now denied, so every built-in function MUST have a code-defined default — this test
/// fails the moment someone adds a [KernelFunction] without one (instead of it being denied in production).
/// </summary>
public class BuiltInToolGovernanceTests
{
    private static readonly Type[] BuiltInPlugins =
        [typeof(WorkflowPlugin), typeof(DocumentPlugin), typeof(RAGPlugin), typeof(AssistantPlugin)];

    private static string[] DeclaredFunctionNames() =>
        BuiltInPlugins
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<KernelFunctionAttribute>()))
            .Where(x => x.Attribute is not null)
            .Select(x => x.Attribute!.Name ?? x.Method.Name)
            .ToArray();

    [Fact]
    public void Every_built_in_kernel_function_has_a_governance_default()
    {
        var missing = DeclaredFunctionNames()
            .Where(name => BuiltInToolGovernance.TryCreateDefault(name, Guid.NewGuid()) is null)
            .ToArray();

        Assert.True(missing.Length == 0,
            $"Built-in functions without a default in BuiltInToolGovernance (they would be denied): {string.Join(", ", missing)}");
    }

    [Fact]
    public void The_default_table_lists_only_functions_that_actually_exist()
    {
        var declared = DeclaredFunctionNames().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = BuiltInToolGovernance.KnownFunctionNames.Where(name => !declared.Contains(name)).ToArray();

        Assert.True(stale.Length == 0, $"Defaults for functions that no longer exist: {string.Join(", ", stale)}");
    }

    [Fact]
    public void Defaults_carry_the_seeded_id_and_risk_and_never_require_approval_or_a_role()
    {
        var tenant = Guid.NewGuid();

        var startWorkflow = BuiltInToolGovernance.TryCreateDefault("start_workflow", tenant)!;

        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000401"), startWorkflow.Id);
        Assert.Equal("Medium", startWorkflow.RiskLevel);
        Assert.Equal(tenant, startWorkflow.TenantId);
        Assert.False(startWorkflow.ApprovalRequired);
        Assert.Null(startWorkflow.RequiredRole);
        Assert.True(startWorkflow.AuditRequired);
        Assert.Equal("Low", BuiltInToolGovernance.TryCreateDefault("search_knowledge_base", tenant)!.RiskLevel);
    }

    [Fact]
    public void Function_names_match_case_insensitively_and_unknown_names_have_no_default()
    {
        Assert.NotNull(BuiltInToolGovernance.TryCreateDefault("START_WORKFLOW", Guid.NewGuid()));
        Assert.Null(BuiltInToolGovernance.TryCreateDefault("delete_everything", Guid.NewGuid()));
    }

    [Theory]
    [InlineData("TimePlugin", true)]
    [InlineData("ConversationSummaryPlugin", true)]
    [InlineData("WorkflowPlugin", false)]
    [InlineData("DynamicTools", false)]
    [InlineData(null, false)]
    public void Only_the_semantic_kernel_utility_plugins_are_exempt(string? plugin, bool expected) =>
        Assert.Equal(expected, BuiltInToolGovernance.IsUtilityPlugin(plugin));
}
