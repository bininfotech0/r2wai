using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Core;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace R2WAI.Infrastructure.AI;

public class SemanticKernelService : IAIService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SemanticKernelService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ModelGateway.IModelGateway _modelGateway;
    private static readonly ConcurrentDictionary<string, (Kernel Kernel, DateTime CreatedAt)> _kernels = new();
    private static readonly TimeSpan KernelMaxAge = TimeSpan.FromHours(1);

    // Latches "this provider doesn't support embeddings" per config, the first time it's
    // observed, so every later call short-circuits instead of repeating the failing request.
    // Root-caused to a specific bad response shape, not just "slow": a local Ollama model that
    // rejects embedding requests drove the API container's memory from ~1.5GB to 6GB+ within
    // about a minute on repeated attempts, even after bounding both the per-call network timeout
    // and the overall operation with a hard CancellationTokenSource — the growth happens well
    // within either bound, so timeouts alone can't prevent it. Skipping the call entirely once
    // the provider is known to reject it avoids the code path that causes the growth, without
    // requiring the actual leak (still undiagnosed) to be fixed first.
    private static readonly ConcurrentDictionary<string, bool> _embeddingsKnownUnsupported = new();

    // Some models (notably smaller/local ones via Ollama) don't reliably emit real structured
    // tool_calls -- instead they print a function-call-shaped JSON object as plain assistant
    // text, which Semantic Kernel has no way to intercept. This detects that leaked shape so we
    // never surface raw JSON to the end user. Different models use different key names for the
    // call's payload ("arguments", "parameters", "params", "input" have all been observed in
    // the wild), so match on the structural shape rather than one specific key name.
    private static readonly Regex LeakedToolCallPattern = new(
        @"^\s*\{\s*""name""\s*:\s*""[^""]+""\s*,\s*""(arguments|parameters|params|input)""\s*:",
        RegexOptions.Compiled);
    private const string LeakedToolCallFallback =
        "I tried to use a tool to answer that, but this model doesn't support tool calls reliably. Could you rephrase your question, or try again with a different AI model?";

    private string SanitizeLeakedToolCall(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || !LeakedToolCallPattern.IsMatch(content))
            return content;

        _logger.LogWarning("Model emitted a leaked pseudo tool-call instead of a real function call: {ToolCallText}", content);
        return LeakedToolCallFallback;
    }

    public SemanticKernelService(
        IConfiguration configuration,
        ILogger<SemanticKernelService> logger,
        IServiceProvider serviceProvider,
        ModelGateway.IModelGateway modelGateway)
    {
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _modelGateway = modelGateway;
    }

    public async Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, CancellationToken ct = default)
    {
        var kernel = await GetOrCreateKernelAsync(ct: ct);

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
        var kernel = await GetOrCreateKernelAsync(ct: ct);
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
        var kernel = await GetOrCreateKernelAsync(ct: ct);
        var prompt = $"Summarize the following text in {maxLength} characters or less:\n\n{TruncateInputText(text)}";

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
        var kernel = await GetOrCreateKernelAsync(ct: ct);
        var prompt = $"Extract data from the following text according to this schema: {schema}\n\nText:\n{TruncateInputText(text)}";

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
        var kernel = await GetOrCreateKernelAsync(ct: ct);
        var prompt = $"Compare the following two documents and provide a detailed analysis of similarities and differences:\n\nDocument 1:\n{TruncateInputText(sourceText)}\n\nDocument 2:\n{TruncateInputText(targetText)}";

        var function = kernel.CreateFunctionFromPrompt(prompt, new OpenAIPromptExecutionSettings
        {
            MaxTokens = 4096,
            Temperature = 0.3
        });

        var result = await kernel.InvokeAsync(function, null, ct);
        return result.ToString();
    }

    public async Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, CancellationToken ct = default)
    {
        var kernel = await GetOrCreateKernelAsync(enableTools, ct);

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
            chatHistory.AddUserMessage(TruncateInputText(conversationHistory, keepEnd: true));

        chatHistory.AddUserMessage(message);

        var result = await chatCompletion.GetChatMessageContentAsync(chatHistory, new OpenAIPromptExecutionSettings
        {
            MaxTokens = 4096,
            Temperature = 0.7,
            FunctionChoiceBehavior = enableTools ? FunctionChoiceBehavior.Auto() : null
        }, kernel, ct);

        return SanitizeLeakedToolCall(result.Content ?? string.Empty);
    }

    public async IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var kernel = await GetOrCreateKernelAsync(enableTools, ct);

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
            chatHistory.AddUserMessage(TruncateInputText(conversationHistory, keepEnd: true));

        chatHistory.AddUserMessage(message);

        var streamingResult = chatCompletion.GetStreamingChatMessageContentsAsync(chatHistory, new OpenAIPromptExecutionSettings
        {
            MaxTokens = 4096,
            Temperature = 0.7,
            FunctionChoiceBehavior = enableTools ? FunctionChoiceBehavior.Auto() : null
        }, kernel, ct);

        // Buffer the start of the reply to detect a leaked pseudo tool-call (see
        // SanitizeLeakedToolCall) before committing to streaming raw chunks to the caller.
        // Legitimate natural-language replies essentially never start with '{', so we can bail
        // out on the very first non-whitespace character in the common case; only a reply that
        // actually starts with '{' pays the cost of buffering further, up to probeCap, to give
        // the pattern room to match past a plugin/function name of realistic length (e.g.
        // "AssistantPlugin-get_assistant_context" alone is ~40 chars).
        const int probeCap = 200;
        var probeBuffer = new StringBuilder();
        var toolCallBuffer = new StringBuilder();
        bool? isLeakedToolCall = null;

        await foreach (var chunk in streamingResult)
        {
            if (chunk.Content is null)
                continue;

            if (isLeakedToolCall is null)
            {
                probeBuffer.Append(chunk.Content);
                var probeSoFar = probeBuffer.ToString();
                var trimmedStart = probeSoFar.TrimStart();

                if (trimmedStart.Length == 0)
                    continue; // only whitespace seen so far, keep buffering

                if (trimmedStart[0] != '{')
                {
                    isLeakedToolCall = false;
                    yield return probeSoFar;
                    continue;
                }

                if (LeakedToolCallPattern.IsMatch(probeSoFar))
                {
                    isLeakedToolCall = true;
                    toolCallBuffer.Append(probeSoFar);
                    continue;
                }

                if (probeSoFar.Length < probeCap)
                    continue; // starts with '{' but not conclusive yet -- keep buffering

                // Reached the cap without matching -- treat as ordinary text (e.g. a reply that
                // legitimately starts with a JSON-like example) and flush what we've buffered.
                isLeakedToolCall = false;
                yield return probeSoFar;
                continue;
            }

            if (isLeakedToolCall == true)
                toolCallBuffer.Append(chunk.Content);
            else
                yield return chunk.Content;
        }

        if (isLeakedToolCall == true)
            yield return SanitizeLeakedToolCall(toolCallBuffer.ToString());
        else if (isLeakedToolCall is null && probeBuffer.Length > 0)
            yield return SanitizeLeakedToolCall(probeBuffer.ToString());
    }

    public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (_embeddingsKnownUnsupported.ContainsKey("default"))
            return [];

        var kernel = await GetOrCreateKernelAsync(ct: ct);
        var embeddingGenerator = kernel.Services.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        if (embeddingGenerator is null)
        {
            _logger.LogWarning("No embedding generator configured. Returning empty embedding.");
            return [];
        }

        try
        {
            var result = await embeddingGenerator.GenerateAsync([text], cancellationToken: ct);
            return result.Count > 0 ? result[0].Vector.ToArray() : [];
        }
        catch (Exception ex) when (LatchIfProviderRejectsEmbeddings(ex))
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        if (_embeddingsKnownUnsupported.ContainsKey("default"))
        {
            _logger.LogDebug("Skipping embeddings call — this provider was already observed to reject embedding requests.");
            return [];
        }

        var kernel = await GetOrCreateKernelAsync(ct: ct);
        var embeddingGenerator = kernel.Services.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        if (embeddingGenerator is null)
        {
            _logger.LogWarning("No embedding generator configured. Returning empty embeddings.");
            return [];
        }

        try
        {
            var result = await embeddingGenerator.GenerateAsync(texts, cancellationToken: ct);
            var vectors = result.Select(e => (IReadOnlyList<float>)e.Vector.ToArray()).ToList();

            // The actual observed failure for this provider isn't a thrown exception here — it's
            // a "successful" response whose vectors are all empty (0 dimensions), which only
            // surfaces later as a Postgres error when something tries to insert a 0-length
            // vector. Latch on that shape directly instead of only on exceptions, since this is
            // the failure mode that was actually observed causing runaway resource use downstream.
            if (vectors.Count > 0 && vectors.All(v => v.Count == 0) && _embeddingsKnownUnsupported.TryAdd("default", true))
                _logger.LogWarning("AI provider returned only empty embedding vectors — treating as unsupported and skipping embedding calls for the rest of this process's lifetime.");

            return vectors;
        }
        catch (Exception ex) when (LatchIfProviderRejectsEmbeddings(ex))
        {
            return [];
        }
    }

    // Matches the specific "this model/server doesn't do embeddings" rejection shape (as opposed
    // to a transient network error, which should keep being retried on future calls rather than
    // latched off permanently). Returning false lets the exception propagate normally for
    // anything that doesn't match, so real transient failures aren't silently swallowed.
    private bool LatchIfProviderRejectsEmbeddings(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        var isUnsupportedRejection = message.Contains("does not support embeddings", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not supported", StringComparison.OrdinalIgnoreCase);

        if (!isUnsupportedRejection)
            return false;

        if (_embeddingsKnownUnsupported.TryAdd("default", true))
            _logger.LogWarning(ex, "AI provider rejected an embeddings request as unsupported — will skip embedding calls for the rest of this process's lifetime instead of repeating a request that can't succeed.");

        return true;
    }

    public async Task<string> AnswerQuestionAsync(string question, string context, CancellationToken ct = default)
    {
        var kernel = await GetOrCreateKernelAsync(ct: ct);
        var prompt = $"Answer the question based on the provided context.\n\nContext:\n{TruncateInputText(context)}\n\nQuestion: {question}\n\nAnswer:";

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
            sb.AppendLine($"Context:\n{TruncateInputText(context)}");

        sb.AppendLine($"\nUser: {prompt}");
        sb.AppendLine("\nAssistant:");

        return sb.ToString();
    }

    // No tokenizer is wired up, so this is a coarse ~4-chars-per-token estimate -- deliberately
    // generous rather than exact, just enough to stop unbounded RAG context/chat history from
    // growing a prompt without limit (cost blowup and, on some providers, an outright request
    // failure once the model's context window is exceeded). RAG context is relevance-ranked
    // top-first, so it's truncated from the end; chat history matters most in its most recent
    // turns, so it's truncated from the start.
    private const int MaxInputChars = 24_000; // ~6k tokens

    private static string TruncateInputText(string? text, bool keepEnd = false)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= MaxInputChars)
            return text ?? string.Empty;

        return keepEnd
            ? "[earlier content truncated]...\n" + text[^MaxInputChars..]
            : text[..MaxInputChars] + "\n...[truncated]";
    }

    private async Task<Kernel> GetOrCreateKernelAsync(bool enableTools = false, CancellationToken ct = default)
    {
        var baseKernel = GetOrCreateBaseKernel();

        // Plugins (and therefore autonomous function-calling) are only attached when the
        // caller explicitly opts in. This keeps mutating tools (start_workflow,
        // submit_approval_request, notify_approver) unreachable from anonymous/public
        // surfaces like the embeddable chatbot widget, which always calls with enableTools: false.
        // Nothing is mutated on the returned kernel in that case, so the shared base kernel can
        // be returned directly -- cloning it on every request (the common case: every anonymous
        // chatbot message, every summarize/generate/embed call) was pure per-request overhead.
        if (!enableTools)
            return baseKernel;

        // The tools path attaches plugins resolved from THIS request's DI scope (they close
        // over scoped services like ICurrentUserService/DbContext), so this kernel can't be
        // cached/reused across requests -- it must be a fresh clone every time.
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

            // Tenant-registered API tools (Integrations/Tools & APIs UI) — the Tool/Integration
            // Registry bridge. Optional (a tenant with none registered gets no extra plugin).
            var dynamicToolFactory = _serviceProvider.GetRequiredService<DynamicTools.DynamicToolFunctionFactory>();
            var dynamicPlugin = await dynamicToolFactory.BuildPluginAsync(ct);
            if (dynamicPlugin is not null)
                kernel.Plugins.Add(dynamicPlugin);

            var auditFilter = _serviceProvider.GetRequiredService<AiFunctionAuditFilter>();
            kernel.FunctionInvocationFilters.Add(auditFilter);
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
        var builder = Kernel.CreateBuilder();

        // Provider selection/configuration (openai/ollama/zai/...) lives behind IModelGateway —
        // this class no longer reads "AI:Provider" or constructs an OpenAIClient itself.
        _modelGateway.ConfigureKernel(builder);

        builder.Plugins.AddFromType<ConversationSummaryPlugin>();
        builder.Plugins.AddFromType<TimePlugin>();

        return builder.Build();
    }
}
