using System.Collections.Concurrent;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.Services;

public sealed class RequestMetricsStore : IRequestMetricsStore
{
    private const int MaxEntries = 50_000;
    private readonly ConcurrentQueue<RequestMetricEntry> _entries = new();

    public void Record(RequestMetricEntry entry)
    {
        _entries.Enqueue(entry);

        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _)) { }
    }

    public RequestMetricsSnapshot GetSnapshot(Guid? tenantId, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        var relevant = _entries
            .Where(e => e.Timestamp >= cutoff && (tenantId is null || e.TenantId == tenantId))
            .ToList();

        var total = relevant.Count;
        var success = relevant.Count(e => e.StatusCode < 400);
        var errors = total - success;
        var apiErrors = relevant.Count(e => e.StatusCode >= 400 && e.Category == RequestMetricCategory.Api);
        var aiErrors = relevant.Count(e => e.StatusCode >= 400 && e.Category == RequestMetricCategory.Ai);
        var aiRequests = relevant.Count(e => e.Category == RequestMetricCategory.Ai);
        var activeUsers = relevant.Where(e => e.UserId.HasValue).Select(e => e.UserId).Distinct().Count();
        var avgLatency = total == 0 ? 0 : relevant.Average(e => e.DurationMs);

        return new RequestMetricsSnapshot(
            TotalRequests: total,
            SuccessCount: success,
            ErrorCount: errors,
            SuccessRate: total == 0 ? 100 : Math.Round(success * 100.0 / total, 1),
            AverageLatencyMs: Math.Round(avgLatency, 1),
            ActiveUsers: activeUsers,
            ApiErrors: apiErrors,
            AiErrors: aiErrors,
            AiRequests: aiRequests);
    }
}
