using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.HealthChecks;

public class AiProviderHealthCheck(ApplicationDbContext dbContext, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            // The app's actually-active provider is AI:Provider (env-configured), not just
            // whatever happens to be flagged IsDefault in the ModelConfigurations catalog.
            // Ollama needs no API key, so treat that provider as usable on its own — otherwise
            // a stale/unrelated seeded catalog row (e.g. a keyless OpenAI entry) makes this
            // check report Degraded even though the server is actually serving chat via Ollama.
            var activeProvider = configuration["AI:Provider"] ?? "openai";
            var activeProviderIsSelfHosted = activeProvider.Equals("ollama", StringComparison.OrdinalIgnoreCase);

            var modelCount = await dbContext.ModelConfigurations
                .CountAsync(m => m.IsActive, ct);

            if (modelCount == 0 && !activeProviderIsSelfHosted)
                return HealthCheckResult.Degraded("No active AI models configured");

            var withKeys = await dbContext.ModelConfigurations
                .CountAsync(m => m.IsActive && (m.ApiKeyEncrypted != null && m.ApiKeyEncrypted != ""), ct);

            var localModels = await dbContext.ModelConfigurations
                .CountAsync(m => m.IsActive && m.Provider == "Ollama", ct);

            var usable = withKeys + localModels;

            if (usable == 0 && !activeProviderIsSelfHosted)
                return HealthCheckResult.Degraded($"{modelCount} models configured but none have API keys set");

            if (usable == 0 && activeProviderIsSelfHosted)
                return HealthCheckResult.Healthy($"Active provider is Ollama at {configuration["AI:Ollama:Endpoint"]} (no API key required)");

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
