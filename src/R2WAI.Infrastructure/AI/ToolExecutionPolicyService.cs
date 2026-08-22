using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI;

public class ToolExecutionPolicyService : IToolExecutionPolicyService
{
    private const string PolicyType = "ToolExecution";
    private readonly IRepository<GlobalPolicy> _policies;

    public ToolExecutionPolicyService(IRepository<GlobalPolicy> policies)
    {
        _policies = policies;
    }

    public async Task<string?> GetMaxRiskLevelAsync(Guid tenantId, CancellationToken ct = default)
    {
        var policy = await _policies.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Type == PolicyType && p.IsActive, ct);

        return ToolExecutionPolicyEvaluator.TryParseMaxRiskLevel(policy?.Content);
    }
}
