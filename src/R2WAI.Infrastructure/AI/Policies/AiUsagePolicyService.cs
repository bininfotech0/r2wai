using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI.Policies;

public class AiUsagePolicyService : IAiUsagePolicyService
{
    private const string PolicyType = "AiUsage";
    // 25h, not 24h: the counter's own key already rotates at UTC midnight (a new key per day), so
    // this expiration is just a safety net against an orphaned key outliving its day if something
    // ever reads/writes it a little early/late across the boundary — it does not extend the window.
    private static readonly TimeSpan CounterExpiration = TimeSpan.FromHours(25);

    private readonly IRepository<GlobalPolicy> _policies;
    private readonly ICacheService _cache;

    public AiUsagePolicyService(IRepository<GlobalPolicy> policies, ICacheService cache)
    {
        _policies = policies;
        _cache = cache;
    }

    private static string CounterKey(Guid tenantId) => $"aiusage:{tenantId}:{DateTime.UtcNow:yyyy-MM-dd}";

    public async Task<bool> IsUnderCapAsync(Guid tenantId, CancellationToken ct = default)
    {
        var cap = await GetDailyCapAsync(tenantId, ct);
        if (cap is null) return true;

        var current = await _cache.GetAsync<CounterValue>(CounterKey(tenantId), ct);
        return (current?.Count ?? 0) < cap.Value;
    }

    public async Task<AiUsageStatus> GetStatusAsync(Guid tenantId, CancellationToken ct = default)
    {
        var cap = await GetDailyCapAsync(tenantId, ct);
        var current = await _cache.GetAsync<CounterValue>(CounterKey(tenantId), ct);
        return new AiUsageStatus(cap, current?.Count ?? 0);
    }

    private async Task<int?> GetDailyCapAsync(Guid tenantId, CancellationToken ct)
    {
        var policy = await _policies.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Type == PolicyType && p.IsActive, ct);

        return AiUsagePolicyEvaluator.TryParseMaxRequestsPerDay(policy?.Content);
    }

    public async Task RecordRequestAsync(Guid tenantId, CancellationToken ct = default)
    {
        var key = CounterKey(tenantId);
        // Best-effort increment (read-then-write, not atomic) — acceptable for a soft usage cap, not
        // a hard security boundary; matches this Policy Engine's existing "additive tightening"
        // philosophy elsewhere. A concurrent burst right at the cap boundary can let a few extra
        // requests through, never fewer than the configured cap.
        var current = await _cache.GetAsync<CounterValue>(key, ct);
        var next = new CounterValue((current?.Count ?? 0) + 1);
        await _cache.SetAsync(key, next, CounterExpiration, ct);
    }

    private sealed class CounterValue
    {
        public int Count { get; set; }
        public CounterValue() { }
        public CounterValue(int count) => Count = count;
    }
}
