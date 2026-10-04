using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.AI;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers AiFunctionAuditFilter.EvaluateGovernance — the pure decision logic behind the Tool/API
/// Gateway's enforcement of ToolDefinition ("Capability") governance fields. Kept free of Semantic
/// Kernel types deliberately so it doesn't need a live Kernel/FunctionInvocationContext to test.
/// </summary>
public class AiFunctionAuditFilterTests
{
    private static ToolDefinition CreateToolDef(string? requiredRole, bool approvalRequired)
    {
        var tool = new ToolDefinition(Guid.NewGuid(), Guid.NewGuid(), "start_workflow", ToolType.SemanticKernelFunction, "test");
        tool.ConfigureGovernance("Medium", requiredRole, confirmationRequired: false, approvalRequired, auditRequired: true);
        return tool;
    }

    [Fact]
    public void EvaluateGovernance_NoToolDefinition_DeniesUnknownTool()
    {
        // Fail closed: this used to be Allow, which silently ungoverned any function with no record
        // (audit finding P0-4). Built-in functions get a code-defined default record before this point.
        var decision = AiFunctionAuditFilter.EvaluateGovernance(null, ["Admin", "SystemAdmin"]);
        Assert.Equal(GovernanceDecision.DenyUnknownTool, decision);
    }

    [Fact]
    public void EvaluateGovernance_NoRequiredRoleNoApproval_Allows()
    {
        // Matches the seeded default for all 14 built-in functions — rollout must not change behavior.
        var toolDef = CreateToolDef(requiredRole: null, approvalRequired: false);
        var decision = AiFunctionAuditFilter.EvaluateGovernance(toolDef, []);
        Assert.Equal(GovernanceDecision.Allow, decision);
    }

    [Fact]
    public void EvaluateGovernance_UserHasRequiredRole_Allows()
    {
        var toolDef = CreateToolDef(requiredRole: "WorkflowManager", approvalRequired: false);
        var decision = AiFunctionAuditFilter.EvaluateGovernance(toolDef, ["User", "WorkflowManager"]);
        Assert.Equal(GovernanceDecision.Allow, decision);
    }

    [Fact]
    public void EvaluateGovernance_UserHasRequiredRole_CaseInsensitive_Allows()
    {
        var toolDef = CreateToolDef(requiredRole: "WorkflowManager", approvalRequired: false);
        var decision = AiFunctionAuditFilter.EvaluateGovernance(toolDef, ["workflowmanager"]);
        Assert.Equal(GovernanceDecision.Allow, decision);
    }

    [Fact]
    public void EvaluateGovernance_UserMissingRequiredRole_DeniesRole()
    {
        var toolDef = CreateToolDef(requiredRole: "WorkflowManager", approvalRequired: false);
        var decision = AiFunctionAuditFilter.EvaluateGovernance(toolDef, ["User"]);
        Assert.Equal(GovernanceDecision.DenyMissingRole, decision);
    }

    [Fact]
    public void EvaluateGovernance_ApprovalRequired_DeniesApproval()
    {
        var toolDef = CreateToolDef(requiredRole: null, approvalRequired: true);
        var decision = AiFunctionAuditFilter.EvaluateGovernance(toolDef, ["Admin"]);
        Assert.Equal(GovernanceDecision.DenyApprovalRequired, decision);
    }

    [Fact]
    public void EvaluateGovernance_MissingRoleTakesPrecedenceOverApprovalRequired()
    {
        var toolDef = CreateToolDef(requiredRole: "Admin", approvalRequired: true);
        var decision = AiFunctionAuditFilter.EvaluateGovernance(toolDef, ["User"]);
        Assert.Equal(GovernanceDecision.DenyMissingRole, decision);
    }

    [Theory]
    [InlineData("get_leave_balance", "Get Leave Balance")]
    [InlineData("SubmitInvoice", "Submit Invoice")]
    [InlineData("start_workflow", "Start Workflow")]
    [InlineData("HTTPRequest", "H T T P Request")]
    [InlineData("ping", "Ping")]
    public void HumanizeFunctionName_ProducesReadableDisplayName(string technicalName, string expected)
    {
        Assert.Equal(expected, AiFunctionAuditFilter.HumanizeFunctionName(technicalName));
    }

    [Fact]
    public void IsEnabledForCallingAssistant_NullList_MeansUnfiltered_Allows()
    {
        // Matches every assistant with no explicit tool selection — must stay allowed, not deny-all.
        Assert.True(AiFunctionAuditFilter.IsEnabledForCallingAssistant(Guid.NewGuid(), null));
    }

    [Fact]
    public void IsEnabledForCallingAssistant_ToolInList_Allows()
    {
        var toolId = Guid.NewGuid();
        Assert.True(AiFunctionAuditFilter.IsEnabledForCallingAssistant(toolId, [toolId, Guid.NewGuid()]));
    }

    [Fact]
    public void IsEnabledForCallingAssistant_ToolNotInList_Denies()
    {
        var toolId = Guid.NewGuid();
        Assert.False(AiFunctionAuditFilter.IsEnabledForCallingAssistant(toolId, [Guid.NewGuid(), Guid.NewGuid()]));
    }

    [Fact]
    public void IsEnabledForCallingAssistant_EmptyList_DeniesEverything()
    {
        // An assistant explicitly configured with zero enabled tools — distinct from null (unfiltered).
        Assert.False(AiFunctionAuditFilter.IsEnabledForCallingAssistant(Guid.NewGuid(), []));
    }
}
