using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.HealthChecks;

/// <summary>
/// Replaces <c>AddDbContextCheck</c> for the "database" health check.
///
/// The built-in DbContextHealthCheck passes the health-check request's own CancellationToken
/// straight into <c>CanConnectAsync</c>. When the caller hangs up — the SPA unmounts, the browser
/// closes the tab, or a readiness probe times out — that token fires and the check returns
/// Unhealthy with "The operation was canceled.". That is not a database fault, but it:
///   - logged an [ERR] line on every routine page navigation, and
///   - could mark a healthy instance Unhealthy for a container orchestrator or a status page
///     that queried /health just as the client disconnected.
///
/// So the reachability probe gets its own short, bounded timeout that is deliberately NOT linked
/// to the caller's token, and the two cancellation cases are reported separately: our own timeout
/// is a genuine Unhealthy, a dead caller is simply abandoned.
/// </summary>
public class DatabaseHealthCheck(
    ApplicationDbContext dbContext,
    ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    /// <summary>
    /// A connect probe against the local network database should be near-instant. If it has not
    /// answered by now, the database genuinely is not responding.
    /// </summary>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        // Deliberately not linked to `ct` — see the class remarks.
        using var timeout = new CancellationTokenSource(ProbeTimeout);

        try
        {
            var reachable = await dbContext.Database.CanConnectAsync(timeout.Token);
            return reachable
                ? HealthCheckResult.Healthy("Database is reachable")
                : HealthCheckResult.Unhealthy("Database did not accept a connection");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogError("Database health check did not respond within {Timeout}ms", ProbeTimeout.TotalMilliseconds);
            return HealthCheckResult.Unhealthy($"Database did not respond within {ProbeTimeout.TotalSeconds:0}s");
        }
        catch (OperationCanceledException)
        {
            // The caller disconnected, not the database. There is nobody left to read this
            // verdict, and reporting Unhealthy would poison a probe result or an error log.
            logger.LogDebug("Database health check abandoned: the requesting client disconnected.");
            return HealthCheckResult.Healthy("Health check abandoned by the caller");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database is unreachable", ex);
        }
    }
}
