using System.Runtime.CompilerServices;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.AI;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers AgentRuntime — a thin, deliberately simple facade over IAIService that always enables
/// tools. The property under test is that callers of IAgentRuntime can never accidentally get a
/// non-agentic (tools-disabled) response the way a raw IAIService.ChatAsync call could, plus the
/// widened contract's own two additions: a correlation id always comes back, and ToolCallsMade
/// reflects this call's own trace, not a prior call's leftover in a shared scope.
/// </summary>
public class AgentRuntimeTests
{
    private sealed class FakeAiService : IAIService
    {
        public bool? LastEnableTools { get; private set; }
        public string? LastMessage { get; private set; }
        public string? LastHistory { get; private set; }
        public string? LastSystemPrompt { get; private set; }
        public IReadOnlyCollection<Guid>? LastEnabledToolIds { get; private set; }
        public Action<ChatTraceCollector>? OnChatAsync { get; set; }

        private readonly ChatTraceCollector _traceCollector;
        public FakeAiService(ChatTraceCollector traceCollector) => _traceCollector = traceCollector;

        public Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, CancellationToken ct = default)
        {
            LastMessage = message;
            LastHistory = conversationHistory;
            LastSystemPrompt = systemPrompt;
            LastEnableTools = enableTools;
            LastEnabledToolIds = enabledToolIds;
            OnChatAsync?.Invoke(_traceCollector);
            return Task.FromResult("invoke-result");
        }

        public async IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, [EnumeratorCancellation] CancellationToken ct = default)
        {
            LastMessage = message;
            LastHistory = conversationHistory;
            LastSystemPrompt = systemPrompt;
            LastEnableTools = enableTools;
            yield return "chunk1";
            yield return "chunk2";
            await Task.CompletedTask;
        }

        public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> SummarizeTextAsync(string text, int maxLength = 500, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> ExtractDataAsync(string text, string schema, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> CompareDocumentsAsync(string sourceText, string targetText, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> AnswerQuestionAsync(string question, string context, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task InvokeAsync_AlwaysPassesEnableToolsTrue()
    {
        var traceCollector = new ChatTraceCollector();
        var fake = new FakeAiService(traceCollector);
        var runtime = new AgentRuntime(fake, traceCollector);

        var result = await runtime.InvokeAsync(new AgentRuntimeRequest("hello", "history", "system prompt"));

        Assert.Equal("invoke-result", result.Text);
        Assert.True(fake.LastEnableTools);
        Assert.Equal("hello", fake.LastMessage);
        Assert.Equal("history", fake.LastHistory);
        Assert.Equal("system prompt", fake.LastSystemPrompt);
    }

    [Fact]
    public async Task InvokeAsync_NoCorrelationIdSupplied_GeneratesOne()
    {
        var traceCollector = new ChatTraceCollector();
        var runtime = new AgentRuntime(new FakeAiService(traceCollector), traceCollector);

        var result = await runtime.InvokeAsync(new AgentRuntimeRequest("hello"));

        Assert.NotEqual(Guid.Empty, result.CorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_CorrelationIdSupplied_IsEchoedBack()
    {
        var traceCollector = new ChatTraceCollector();
        var runtime = new AgentRuntime(new FakeAiService(traceCollector), traceCollector);
        var correlationId = Guid.NewGuid();

        var result = await runtime.InvokeAsync(new AgentRuntimeRequest("hello", CorrelationId: correlationId));

        Assert.Equal(correlationId, result.CorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_ToolsCalledDuringThisTurn_AppearInToolCallsMade()
    {
        var traceCollector = new ChatTraceCollector();
        var fake = new FakeAiService(traceCollector)
        {
            // Simulates what ToolGateway does mid-ChatAsync: record a call before returning.
            OnChatAsync = tc => tc.RecordFunctionCall("DynamicTools", "get_status", null, 5, true, null)
        };
        var runtime = new AgentRuntime(fake, traceCollector);

        var result = await runtime.InvokeAsync(new AgentRuntimeRequest("hello"));

        Assert.Equal(["get_status"], result.ToolCallsMade);
    }

    [Fact]
    public async Task InvokeAsync_PriorCallsTraceInTheSameScope_DoesNotLeakIntoThisResult()
    {
        var traceCollector = new ChatTraceCollector();
        traceCollector.RecordFunctionCall("DynamicTools", "leftover_from_earlier_call", null, 1, true, null);
        var fake = new FakeAiService(traceCollector);
        var runtime = new AgentRuntime(fake, traceCollector);

        var result = await runtime.InvokeAsync(new AgentRuntimeRequest("hello"));

        Assert.Empty(result.ToolCallsMade);
    }

    [Fact]
    public async Task StreamAsync_AlwaysPassesEnableToolsTrue_AndYieldsAllChunks()
    {
        var traceCollector = new ChatTraceCollector();
        var fake = new FakeAiService(traceCollector);
        var runtime = new AgentRuntime(fake, traceCollector);

        var chunks = new List<string>();
        await foreach (var chunk in runtime.StreamAsync(new AgentRuntimeRequest("hello")))
            chunks.Add(chunk);

        Assert.Equal(["chunk1", "chunk2"], chunks);
        Assert.True(fake.LastEnableTools);
    }
}
