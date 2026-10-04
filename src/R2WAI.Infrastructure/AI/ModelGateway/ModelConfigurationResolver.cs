using Microsoft.Extensions.Logging;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Validation;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI.ModelGateway;

public class ModelConfigurationResolver(
    IRepository<ModelConfiguration> repository,
    IEncryptionService encryption,
    ILogger<ModelConfigurationResolver> logger) : IModelConfigurationResolver
{
    public async Task<ResolvedModelConfig?> ResolveAsync(Guid? modelConfigurationId, Guid tenantId, CancellationToken ct = default)
    {
        ModelConfiguration? config;
        if (modelConfigurationId is null)
        {
            // No explicit model chosen — use the tenant's admin-marked default from the AI Models
            // catalog instead of silently falling straight to the process-wide "AI:Provider" config.
            // That catalog (and its "Default" toggle) previously had no runtime effect here at all.
            config = await repository.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.IsDefault && m.IsActive, ct);
            if (config is null)
                return null;
        }
        else
        {
            config = await repository.GetByIdAsync(modelConfigurationId.Value, ct);
            if (config is null || config.TenantId != tenantId || !config.IsActive)
            {
                logger.LogWarning(
                    "ModelConfiguration {Id} could not be resolved for tenant {TenantId} (missing, inactive, or belongs to a different tenant) — falling back to the global AI provider.",
                    modelConfigurationId, tenantId);
                return null;
            }
        }

        // Additive tightening, mirrors CreateModelCommandValidator's write-time check — a policy
        // or classification change made after this config was created must not silently let a
        // Confidential/Restricted-classified chat leave the tenant via a non-local provider.
        if (ValidationExtensions.ViolatesDataClassificationBoundary(config.DataClassification.ToString(), config.Provider))
        {
            logger.LogWarning(
                "ModelConfiguration {Id} violates its data-classification boundary at resolve time (Provider={Provider}, Classification={Classification}) — falling back to the global AI provider.",
                modelConfigurationId, config.Provider, config.DataClassification);
            return null;
        }

        string? apiKey = null;
        if (!string.IsNullOrEmpty(config.ApiKeyEncrypted))
        {
            try
            {
                apiKey = encryption.Decrypt(config.ApiKeyEncrypted);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to decrypt ApiKeyEncrypted for ModelConfiguration {Id} — falling back to the global AI provider.",
                    modelConfigurationId);
                return null;
            }
        }

        return new ResolvedModelConfig(config.Provider, apiKey, config.ModelId, config.Endpoint,
            config.MaxTokens, config.Temperature, config.TopP);
    }
}
