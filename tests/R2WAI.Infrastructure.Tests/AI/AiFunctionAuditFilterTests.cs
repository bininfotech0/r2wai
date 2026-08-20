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
    public void EvaluateGovernance_NoToolDefinition_Allows()
    {
        var decision = AiFunctionAuditFilter.EvaluateGovernance(null, []);
        Assert.Equal(GovernanceDecision.Allow, decision);
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
}
