using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI.Policies;

public class PiiPolicyService : IPiiPolicyService
{
    private const string PolicyType = "Pii";

    private readonly IRepository<GlobalPolicy> _policies;

    public PiiPolicyService(IRepository<GlobalPolicy> policies)
    {
        _policies = policies;
    }

    public async Task<PiiCheckResult> CheckAsync(string text, Guid tenantId, CancellationToken ct = default)
    {
        var policy = await _policies.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Type == PolicyType && p.IsActive, ct);

        var action = PiiPolicyEvaluator.TryParseAction(policy?.Content);
        if (action is null)
            return new PiiCheckResult(false, text, []);

        var matches = PiiDetector.Find(text);
        if (matches.Count == 0)
            return new PiiCheckResult(false, text, []);

        var types = matches.Select(m => m.Type).Distinct().ToList();

        if (action == "block")
            return new PiiCheckResult(true, text, types);

        // action == "redact"
        return new PiiCheckResult(false, PiiDetector.Redact(text), types);
    }
}
