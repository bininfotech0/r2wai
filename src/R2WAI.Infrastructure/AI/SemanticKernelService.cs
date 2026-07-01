using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Core;
using OpenAI;
using System.ClientModel;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace R2WAI.Infrastructure.AI;

public class SemanticKernelService : IAIService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SemanticKernelService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private static readonly ConcurrentDictionary<string, (Kernel Kernel, DateTime CreatedAt)> _kernels = new();
    private static readonly TimeSpan KernelMaxAge = TimeSpan.FromHours(1);

    public SemanticKernelService(
        IConfiguration configuration,
        ILogger<SemanticKernelService> logger,
        IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, CancellationToken ct = default)
    {
        var kernel = GetOrCreateKernel();

        if (kernel.Services.GetService<IChatCompletionService>() is null)
        {
            _logger.LogWarning("No AI chat completion service is configured for response generation.");
            return "AI service is not configured. Please set up an AI provider in the application settings.";
        }

        var fullPrompt = BuildPrompt(prompt, systemPrompt, context);

        var function = kernel.CreateFunctionFromPrompt(fullPrompt, new OpenAIPromptExecutionSettings
        {
            MaxTokens = 2048,
            Temperature = 0.7
        });

        var result = await kernel.InvokeAsync(function, null, ct);
        return result.ToString();
    }

    public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, string? systemPrompt = null, string? context = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var kernel = GetOrCreateKernel();
        var fullPrompt = BuildPrompt(prompt, systemPrompt, context);

        var function = kernel.CreateFunctionFromPrompt(fullPrompt, new OpenAIPromptExecutionSettings
        {
            MaxTokens = 4096,
            Temperature = 0.7
        });

        var streamingResult = kernel.InvokeStreamingAsync<StreamingChatMessageContent>(function, null, ct);

        await foreach (var chunk in streamingResult)
        {
            if (chunk.Content is not null)
                yield return chunk.Content;
        }
    }

    public async Task<string> SummarizeTextAsync(string text, int maxLength = 500, CancellationToken ct = default)
    {
        var kernel = GetOrCreateKernel();
        var prompt = $"Summarize the following text in {maxLength} characters or less:\n\n{text}";

        var function = kernel.CreateFunctionFromPrompt(prompt, new OpenAIPromptExecutionSettings
        {
            MaxTokens = maxLength,
            Temperature = 0.3
        });

        var result = await kernel.InvokeAsync(function, null, ct);
        return result.ToString();
    }

    public async Task<string> ExtractDataAsync(string text, string schema, CancellationToken ct = default)
    {
        var kernel = GetOrCreateKernel();
        var prompt = $"Extract data from the following text according to this schema: {schema}\n\nText:\n{text}";

        var function = kernel.CreateFunctionFromPrompt(prompt, new OpenAIPromptExecutionSettings
        {
            MaxTokens = 2048,
            Temperature = 0.1
        });

        var result = await kernel.InvokeAsync(function, null, ct);
        return result.ToString();
    }

    public async Task<string> CompareDocumentsAsync(string sourceText, string targetText, CancellationToken ct = default)
    {
        var kernel = GetOrCreateKernel();
        var prompt = $"Compare the following two documents and provide a detailed analysis of similarities and differences:\n\nDocument 1:\n{sourceText}\n\nDocument 2:\n{targetText}";

        var function = kernel.CreateFunctionFromPrompt(prompt, new OpenAIPromptExecutionSettings
        {
            MaxTokens = 4096,
            Temperature = 0.3
        });

        var result = await kernel.InvokeAsync(function, null, ct);
        return result.ToString();
    }

    public async Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, CancellationToken ct = default)
    {
        var kernel = GetOrCreateKernel();

        var chatCompletion = kernel.Services.GetService<IChatCompletionService>();
        if (chatCompletion is null)
        {
            _logger.LogWarning("No AI chat completion service is configured. Configure AI:OpenAI:ApiKey or AI:Provider=ollama.");
            return "AI service is not configured. Please set up an AI provider (OpenAI API key or Ollama) in the application settings to enable chat.";
        }

        var chatHistory = new ChatHistory();

        if (!string.IsNullOrEmpty(systemPrompt))
            chatHistory.AddSystemMessage(systemPrompt);
        else
            chatHistory.AddSystemMessage("You are R2WAI, an intelligent enterprise AI assistant specialized in work execution, approvals, and document intelligence.");

        if (!string.IsNullOrEmpty(conversationHistory))
            chatHistory.AddUserMessage(conversationHistory);

        chatHistory.AddUserMessage(message);

        var result = await chatCompletion.GetChatMessageContentAsync(chatHistory, new OpenAIPromptExecutionSettings
        {
            MaxTokens = 4096,
            Temperature = 0.7
        }, kernel, ct);

        return result.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var kernel = GetOrCreateKernel();

        var chatCompletion = kernel.Services.GetService<IChatCompletionService>();
        if (chatCompletion is null)
        {
            _logger.LogWarning("No AI chat completion service is configured for streaming.");
            yield return "AI service is not configured. Please set up an AI provider in the application settings.";
            yield break;
        }

        var chatHistory = new ChatHistory();

        if (!string.IsNullOrEmpty(systemPrompt))
            chatHistory.AddSystemMessage(systemPrompt);
        else
            chatHistory.AddSystemMessage("You are R2WAI, an intelligent enterprise AI assistant specialized in work execution, approvals, and document intelligence.");

        if (!string.IsNullOrEmpty(conversationHistory))
            chatHistory.AddUserMessage(conversationHistory);

        chatHistory.AddUserMessage(message);

        var streamingResult = chatCompletion.GetStreamingChatMessageContentsAsync(chatHistory, new OpenAIPromptExecutionSettings
        {
            MaxTokens = 4096,
            Temperature = 0.7
        }, kernel, ct);

        await foreach (var chunk in streamingResult)
        {
            if (chunk.Content is not null)
                yield return chunk.Content;
        }
    }

    public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var kernel = GetOrCreateKernel();
        var embeddingGenerator = kernel.Services.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        if (embeddingGenerator is null)
        {
            _logger.LogWarning("No embedding generator configured. Returning empty embedding.");
            return [];
        }

        var result = await embeddingGenerator.GenerateAsync([text], cancellationToken: ct);
        return result.Count > 0 ? result[0].Vector.ToArray() : [];
    }

    public async Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var kernel = GetOrCreateKernel();
        var embeddingGenerator = kernel.Services.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        if (embeddingGenerator is null)
        {
            _logger.LogWarning("No embedding generator configured. Returning empty embeddings.");
            return [];
        }

        var result = await embeddingGenerator.GenerateAsync(texts, cancellationToken: ct);
        return result.Select(e => (IReadOnlyList<float>)e.Vector.ToArray()).ToList();
    }

    public async Task<string> AnswerQuestionAsync(string question, string context, CancellationToken ct = default)
    {
        var kernel = GetOrCreateKernel();
        var prompt = $"Answer the question based on the provided context.\n\nContext:\n{context}\n\nQuestion: {question}\n\nAnswer:";

        var function = kernel.CreateFunctionFromPrompt(prompt, new OpenAIPromptExecutionSettings
        {
            MaxTokens = 1024,
            Temperature = 0.3
        });

        var result = await kernel.InvokeAsync(function, null, ct);
        return result.ToString();
    }

    private string BuildPrompt(string prompt, string? systemPrompt, string? context)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(systemPrompt))
            sb.AppendLine($"System: {systemPrompt}");

        if (!string.IsNullOrEmpty(context))
            sb.AppendLine($"Context:\n{context}");

        sb.AppendLine($"\nUser: {prompt}");
        sb.AppendLine("\nAssistant:");

        return sb.ToString();
    }

    private Kernel GetOrCreateKernel()
    {
        var baseKernel = GetOrCreateBaseKernel();

        var kernel = baseKernel.Clone();

        try
        {
            var workflowPlugin = _serviceProvider.GetRequiredService<AI.Plugins.WorkflowPlugin>();
            kernel.Plugins.AddFromObject(workflowPlugin);

            var documentPlugin = _serviceProvider.GetRequiredService<AI.Plugins.DocumentPlugin>();
            kernel.Plugins.AddFromObject(documentPlugin);

            var ragPlugin = _serviceProvider.GetRequiredService<AI.Plugins.RAGPlugin>();
            kernel.Plugins.AddFromObject(ragPlugin);

            var assistantPlugin = _serviceProvider.GetRequiredService<AI.Plugins.AssistantPlugin>();
            kernel.Plugins.AddFromObject(assistantPlugin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load some SK plugins; continuing with built-in plugins only");
        }

        return kernel;
    }

    private Kernel GetOrCreateBaseKernel()
    {
        var configKey = "default";

        if (_kernels.TryGetValue(configKey, out var existing) && DateTime.UtcNow - existing.CreatedAt < KernelMaxAge)
            return existing.Kernel;

        if (existing.Kernel is not null)
            _kernels.TryRemove(configKey, out _);

        return _kernels.GetOrAdd(configKey, _ => (CreateKernel(), DateTime.UtcNow)).Kernel;
    }

    private Kernel CreateKernel()
    {
        {
            var builder = Kernel.CreateBuilder();

            var provider = (_configuration["AI:Provider"] ?? "openai").ToLowerInvariant();

            if (provider == "ollama")
            {
                var ollamaEndpoint = _configuration["AI:Ollama:Endpoint"]
                    ?? throw new InvalidOperationException("AI:Ollama:Endpoint must be configured when using Ollama provider.");
                var ollamaModel = _configuration["AI:Ollama:ModelId"] ?? "qwen2.5-coder:7b";
                var embeddingModel = _configuration["AI:Ollama:EmbeddingModel"] ?? ollamaModel;
                var ollamaV1 = new Uri($"{ollamaEndpoint.TrimEnd('/')}/v1");

                var ollamaClient = new OpenAIClient(new ApiKeyCredential("ollama"), new OpenAIClientOptions { Endpoint = ollamaV1 });
                builder.AddOpenAIChatCompletion(ollamaModel, ollamaClient);
                builder.AddOpenAIEmbeddingGenerator(embeddingModel, ollamaClient);

                _logger.LogInformation("AI provider: Ollama at {Endpoint}, model: {Model}", ollamaEndpoint, ollamaModel);
            }
            else
            {
                var apiKey = _configuration["AI:OpenAI:ApiKey"] ?? _configuration["OpenAI:ApiKey"] ?? string.Empty;
                var modelId = _configuration["AI:OpenAI:ModelId"] ?? "gpt-4o";
                var endpoint = _configuration["AI:OpenAI:Endpoint"];

                if (!string.IsNullOrEmpty(apiKey))
                {
                    if (!string.IsNullOrEmpty(endpoint))
                    {
                        var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
                        builder.AddOpenAIChatCompletion(modelId, client);
                        builder.AddOpenAIEmbeddingGenerator("text-embedding-3-small", client);
                    }
                    else
                    {
                        builder.AddOpenAIChatCompletion(modelId, apiKey);
                        builder.AddOpenAIEmbeddingGenerator("text-embedding-3-small", apiKey);
                    }

                    _logger.LogInformation("AI provider: OpenAI, model: {Model}", modelId);
                }
                else
                {
                    _logger.LogWarning("No AI provider configured. Set AI:OpenAI:ApiKey or AI:Provider=ollama");
                }
            }

            builder.Plugins.AddFromType<ConversationSummaryPlugin>();
            builder.Plugins.AddFromType<TimePlugin>();

            return builder.Build();
        }
    }
}
