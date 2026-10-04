using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.AI.ModelGateway;

public class ModelGateway : IModelGateway
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ModelGateway> _logger;
    private readonly IReadOnlyDictionary<string, IModelProvider> _providers;

    public string ActiveProviderName => (_configuration["AI:Provider"] ?? "openai").ToLowerInvariant();

    public string? FallbackProviderName
    {
        get
        {
            var name = _configuration["AI:FallbackProvider"];
            return string.IsNullOrWhiteSpace(name) ? null : name.ToLowerInvariant();
        }
    }

    public ModelGateway(IConfiguration configuration, ILogger<ModelGateway> logger, IEnumerable<IModelProvider> providers)
    {
        _configuration = configuration;
        _logger = logger;
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public void ConfigureKernel(IKernelBuilder builder, ResolvedModelConfig? config = null)
    {
        // A resolved config picks its own provider (e.g. a tenant's OpenAI ModelConfiguration even
        // though the process-wide default is "ollama") instead of always reading "AI:Provider".
        var providerName = config?.Provider.ToLowerInvariant() ?? ActiveProviderName;

        if (!_providers.TryGetValue(providerName, out var provider))
        {
            _logger.LogWarning("Unrecognized AI provider '{Provider}' — falling back to openai.", providerName);
            provider = _providers["openai"];
        }

        provider.Configure(builder, config);
    }
}
