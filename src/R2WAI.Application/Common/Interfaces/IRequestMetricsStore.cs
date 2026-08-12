namespace R2WAI.Application.Common.Interfaces;

public enum RequestMetricCategory { Api, Ai }

public record RequestMetricEntry(
    DateTime Timestamp,
    Guid? TenantId,
    Guid? UserId,
    string Path,
    int StatusCode,
    long DurationMs,
    RequestMetricCategory Category);

public record RequestMetricsSnapshot(
    int TotalRequests,
    int SuccessCount,
    int ErrorCount,
    double SuccessRate,
    double AverageLatencyMs,
    int ActiveUsers,
    int ApiErrors,
    int AiErrors);

/// <summary>
/// Rolling, in-memory window of real HTTP request outcomes (recorded by
/// RequestLoggingMiddleware for every /api/ request) that powers the Operations
/// Overview cards. Not persisted - a single-instance, best-effort operational
/// signal, not an audit trail.
/// </summary>
public interface IRequestMetricsStore
{
    void Record(RequestMetricEntry entry);
    RequestMetricsSnapshot GetSnapshot(Guid? tenantId, TimeSpan window);
}
