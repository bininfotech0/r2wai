using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace R2WAI.Infrastructure.AI.ModelGateway;

public class ModelGateway : IModelGateway
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ModelGateway> _logger;
    private readonly IReadOnlyDictionary<string, IModelProvider> _providers;

    public string ActiveProviderName => (_configuration["AI:Provider"] ?? "openai").ToLowerInvariant();

    public ModelGateway(IConfiguration configuration, ILogger<ModelGateway> logger, IEnumerable<IModelProvider> providers)
    {
        _configuration = configuration;
        _logger = logger;
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public void ConfigureKernel(IKernelBuilder builder)
    {
        if (!_providers.TryGetValue(ActiveProviderName, out var provider))
        {
            _logger.LogWarning("Unrecognized AI:Provider '{Provider}' — falling back to openai.", ActiveProviderName);
            provider = _providers["openai"];
        }

        provider.Configure(builder);
    }
}
