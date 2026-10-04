using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI.Policies;

public class KnowledgePolicyService : IKnowledgePolicyService
{
    private const string PolicyType = "Knowledge";
    private readonly IRepository<GlobalPolicy> _policies;

    public KnowledgePolicyService(IRepository<GlobalPolicy> policies)
    {
        _policies = policies;
    }

    public async Task<string?> GetMaxClassificationAsync(Guid tenantId, CancellationToken ct = default)
    {
        var policy = await _policies.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Type == PolicyType && p.IsActive, ct);

        return KnowledgePolicyEvaluator.TryParseMaxClassification(policy?.Content);
    }
}
