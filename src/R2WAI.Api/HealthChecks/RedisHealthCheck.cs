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
            var redis = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(connStr);
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
