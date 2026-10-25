using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;
using OpenAI.Chat;
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

        _logger.LogInformation("AI provider: Ollama at {Origin}, model: {Model}", R2WAI.Infrastructure.Security.EgressGuard.SafeUrlForLog(ollamaEndpoint), ollamaModel);
    }

    public void DisableReasoning(OpenAIPromptExecutionSettings settings, string? replyJsonSchema)
    {
        // Ollama's OpenAI-compatible endpoint reads reasoning_effort "none" as think=false, which a
        // hybrid model's template honours (the original qwen3 adds an empty think block). A model
        // that always reasons (qwen3:4b, now the 2507 thinking build) ignores it and reasons in its
        // answer instead, so the reply's schema also goes in as response_format: Ollama compiles it
        // into a grammar, and the first token the model can produce is the JSON itself.
        // A ChatReasoningEffortLevel, not the string: SK only converts the strings OpenAI accepts.
#pragma warning disable OPENAI001 // Experimental in the OpenAI SDK; only its wire value matters here.
        settings.ReasoningEffort = new ChatReasoningEffortLevel("none");
#pragma warning restore OPENAI001
        if (replyJsonSchema is not null)
            settings.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("reply", BinaryData.FromString(replyJsonSchema));
    }
}
