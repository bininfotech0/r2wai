using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace R2WAI.Infrastructure.AI.ModelGateway;

/// <summary>Default provider. Also the fallback when "AI:Provider" is unset or unrecognized.</summary>
public class OpenAiModelProvider : IModelProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiModelProvider> _logger;

    public string Name => "openai";

    public OpenAiModelProvider(IConfiguration configuration, ILogger<OpenAiModelProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void Configure(IKernelBuilder builder)
    {
        var apiKey = _configuration["AI:OpenAI:ApiKey"] ?? _configuration["OpenAI:ApiKey"] ?? string.Empty;
        var modelId = _configuration["AI:OpenAI:ModelId"] ?? "gpt-4o";
        var endpoint = _configuration["AI:OpenAI:Endpoint"];

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("No AI provider configured. Set AI:OpenAI:ApiKey or AI:Provider=ollama");
            return;
        }

        var clientOptions = new OpenAIClientOptions { RetryPolicy = new ClientRetryPolicy(3), NetworkTimeout = ModelGatewayDefaults.CloudNetworkTimeout };
        if (!string.IsNullOrEmpty(endpoint))
            clientOptions.Endpoint = new Uri(endpoint);

        var client = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
        builder.AddOpenAIChatCompletion(modelId, client);
        builder.AddOpenAIEmbeddingGenerator("text-embedding-3-small", client);

        _logger.LogInformation("AI provider: OpenAI, model: {Model}", modelId);
    }
}
