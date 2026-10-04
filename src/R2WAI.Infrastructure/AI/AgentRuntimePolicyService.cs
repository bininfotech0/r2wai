using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI;

public class AgentRuntimePolicyService : IAgentRuntimePolicyService
{
    private const string PolicyType = "AgentRuntime";
    private readonly IRepository<GlobalPolicy> _policies;

    public AgentRuntimePolicyService(IRepository<GlobalPolicy> policies)
    {
        _policies = policies;
    }

    public async Task<AgentRuntimeKind> GetRuntimeAsync(Guid tenantId, CancellationToken ct = default)
    {
        var policy = await _policies.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Type == PolicyType && p.IsActive, ct);

        return Enum.TryParse<AgentRuntimeKind>(policy?.Content, ignoreCase: true, out var kind)
            ? kind
            : AgentRuntimeKind.SemanticKernel;
    }
}
