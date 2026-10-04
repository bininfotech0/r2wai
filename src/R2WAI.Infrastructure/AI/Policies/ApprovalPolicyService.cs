using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI.Policies;

public class ApprovalPolicyService : IApprovalPolicyService
{
    private const string PolicyType = "Approval";
    private readonly IRepository<GlobalPolicy> _policies;

    public ApprovalPolicyService(IRepository<GlobalPolicy> policies)
    {
        _policies = policies;
    }

    public async Task<string?> GetRequireApprovalAboveRiskLevelAsync(Guid tenantId, CancellationToken ct = default)
    {
        var policy = await _policies.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Type == PolicyType && p.IsActive, ct);

        return ApprovalPolicyEvaluator.TryParseRequireApprovalAboveRiskLevel(policy?.Content);
    }
}
