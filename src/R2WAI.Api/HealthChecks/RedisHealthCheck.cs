using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace R2WAI.Api.HealthChecks;

public class RedisHealthCheck(IConfiguration configuration, ILogger<RedisHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var connStr = configuration["Cache:Redis:ConnectionString"]
            ?? configuration.GetConnectionString("Redis");
        if (string.IsNullOrEmpty(connStr))
            return HealthCheckResult.Healthy("Redis not configured");

        try
        {
            // Bounded ConnectTimeout: an unbounded Connect/ConnectAsync against an unreachable Redis
            // can hang for well over a minute on StackExchange.Redis's default retry/backoff (see
            // RedisCacheService's matching fix) — this endpoint backs /health and /health/ready, so
            // that hang would make the whole app look unresponsive to a readiness probe, not just
            // report Redis as down.
            var options = StackExchange.Redis.ConfigurationOptions.Parse(connStr);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 3000;
            options.ConnectRetry = 1;
            options.SyncTimeout = 1000;
            var redis = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(options);
            await redis.GetDatabase().PingAsync();
            await redis.CloseAsync();
            return HealthCheckResult.Healthy("Redis is reachable");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis health check failed");
            return HealthCheckResult.Unhealthy("Redis is unreachable", ex);
        }
    }
}
