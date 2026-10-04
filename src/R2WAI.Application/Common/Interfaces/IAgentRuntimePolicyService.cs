namespace R2WAI.Application.Common.Interfaces;

public enum AgentRuntimeKind
{
    SemanticKernel,
    AgentFramework,
}

/// <summary>Which <see cref="IAgentRuntime"/> backing a tenant's agentic calls use — implementation
/// plan Phase 4's feature flag, backed by the same <c>GlobalPolicy</c> table
/// <c>IToolExecutionPolicyService</c>/<c>IApprovalPolicyService</c> already use for tenant policy
/// settings, not a new entity.</summary>
public interface IAgentRuntimePolicyService
{
    /// <summary>Defaults to <see cref="AgentRuntimeKind.SemanticKernel"/> when no active policy is
    /// configured, or its content doesn't parse as a known runtime name — fail to the proven
    /// runtime, never to the newer one.</summary>
    Task<AgentRuntimeKind> GetRuntimeAsync(Guid tenantId, CancellationToken ct = default);
}
