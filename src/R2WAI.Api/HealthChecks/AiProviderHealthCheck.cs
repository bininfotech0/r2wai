using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.HealthChecks;

public class AiProviderHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var modelCount = await dbContext.ModelConfigurations
                .CountAsync(m => m.IsActive, ct);

            if (modelCount == 0)
                return HealthCheckResult.Degraded("No active AI models configured");

            var withKeys = await dbContext.ModelConfigurations
                .CountAsync(m => m.IsActive && (m.ApiKeyEncrypted != null && m.ApiKeyEncrypted != ""), ct);

            var localModels = await dbContext.ModelConfigurations
                .CountAsync(m => m.IsActive && m.Provider == "Ollama", ct);

            var usable = withKeys + localModels;

            if (usable == 0)
                return HealthCheckResult.Degraded($"{modelCount} models configured but none have API keys set");

            var hasDefault = await dbContext.ModelConfigurations
                .AnyAsync(m => m.IsActive && m.IsDefault, ct);

            var data = new Dictionary<string, object>
            {
                ["totalModels"] = modelCount,
                ["usableModels"] = usable,
                ["hasDefault"] = hasDefault,
            };

            if (!hasDefault)
                return HealthCheckResult.Healthy($"{usable} usable models (no default set)", data);

            return HealthCheckResult.Healthy($"{usable} AI models ready", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to check AI providers", ex);
        }
    }
}
