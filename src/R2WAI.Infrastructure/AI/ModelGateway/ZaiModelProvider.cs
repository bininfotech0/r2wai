using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OpenAI;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace R2WAI.Infrastructure.AI.ModelGateway;

/// <summary>
/// Z.ai models (e.g. GLM-5.2) via any OpenAI-compatible hosted endpoint (e.g. NVIDIA NIM).
/// Chat/reasoning only — no embedding model is configured for this provider.
/// </summary>
public class ZaiModelProvider : IModelProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ZaiModelProvider> _logger;

    public string Name => "zai";

    public ZaiModelProvider(IConfiguration configuration, ILogger<ZaiModelProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void Configure(IKernelBuilder builder, ResolvedModelConfig? config = null)
    {
        var zaiApiKey = config?.ApiKey ?? _configuration["AI:ZAI:ApiKey"]
            ?? throw new ConfigurationException("AI:ZAI:ApiKey must be configured when using the zai provider.");
        var zaiEndpoint = config?.Endpoint ?? _configuration["AI:ZAI:Endpoint"] ?? "https://integrate.api.nvidia.com/v1";
        var zaiModel = config?.ModelId ?? _configuration["AI:ZAI:ModelId"] ?? "z-ai/glm-5.2";

        var zaiClient = new OpenAIClient(new ApiKeyCredential(zaiApiKey), new OpenAIClientOptions { Endpoint = new Uri(zaiEndpoint), RetryPolicy = new ClientRetryPolicy(3), NetworkTimeout = ModelGatewayDefaults.CloudNetworkTimeout });
        builder.AddOpenAIChatCompletion(zaiModel, zaiClient);

        _logger.LogInformation("AI provider: Z.ai at {Endpoint}, model: {Model}", zaiEndpoint, zaiModel);
    }
}
