using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace R2WAI.Api.HealthChecks;

public class MemoryHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private readonly long _thresholdBytes = (configuration.GetValue<long?>("HealthChecks:MemoryThresholdMb") ?? 512) * 1024 * 1024;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var allocated = GC.GetTotalMemory(false);

        if (allocated > _thresholdBytes)
            return Task.FromResult(HealthCheckResult.Degraded($"Memory usage too high: {allocated / 1024 / 1024} MB"));

        return Task.FromResult(HealthCheckResult.Healthy($"Memory: {allocated / 1024 / 1024} MB"));
    }
}
