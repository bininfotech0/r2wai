using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OpenAI;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace R2WAI.Infrastructure.AI.ModelGateway;

/// <summary>Self-hosted local LLM via Ollama's OpenAI-compatible endpoint.</summary>
public class OllamaModelProvider : IModelProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaModelProvider> _logger;

    public string Name => "ollama";

    public OllamaModelProvider(IConfiguration configuration, ILogger<OllamaModelProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void Configure(IKernelBuilder builder, ResolvedModelConfig? config = null)
    {
        var ollamaEndpoint = config?.Endpoint ?? _configuration["AI:Ollama:Endpoint"]
            ?? throw new ConfigurationException("AI:Ollama:Endpoint must be configured when using Ollama provider.");
        var ollamaModel = config?.ModelId ?? _configuration["AI:Ollama:ModelId"] ?? "qwen3:4b";
        var embeddingModel = _configuration["AI:Ollama:EmbeddingModel"] ?? ollamaModel;
        var ollamaV1 = new Uri($"{ollamaEndpoint.TrimEnd('/')}/v1");

        // Retries=1 (not the SDK default of several): a local Ollama model that rejects a
        // request (e.g. "this model doesn't support embeddings") returns the same
        // deterministic error every time, so retrying it repeatedly only multiplies
        // latency and resource use for zero chance of success.
        var ollamaClient = new OpenAIClient(new ApiKeyCredential("ollama"), new OpenAIClientOptions { Endpoint = ollamaV1, RetryPolicy = new ClientRetryPolicy(1), NetworkTimeout = ModelGatewayDefaults.OllamaNetworkTimeout });
        builder.AddOpenAIChatCompletion(ollamaModel, ollamaClient);
        builder.AddOpenAIEmbeddingGenerator(embeddingModel, ollamaClient);

        _logger.LogInformation("AI provider: Ollama at {Endpoint}, model: {Model}", ollamaEndpoint, ollamaModel);
    }
}
