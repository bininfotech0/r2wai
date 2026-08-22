using System.Runtime.CompilerServices;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.AI;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers AgentRuntime — a thin, deliberately simple facade over IAIService that always enables
/// tools. The property under test is that callers of IAgentRuntime can never accidentally get a
/// non-agentic (tools-disabled) response the way a raw IAIService.ChatAsync call could.
/// </summary>
public class AgentRuntimeTests
{
    private sealed class FakeAiService : IAIService
    {
        public bool? LastEnableTools { get; private set; }
        public string? LastMessage { get; private set; }
        public string? LastHistory { get; private set; }
        public string? LastSystemPrompt { get; private set; }

        public Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, CancellationToken ct = default)
        {
            LastMessage = message;
            LastHistory = conversationHistory;
            LastSystemPrompt = systemPrompt;
            LastEnableTools = enableTools;
            return Task.FromResult("invoke-result");
        }

        public async IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, [EnumeratorCancellation] CancellationToken ct = default)
        {
            LastMessage = message;
            LastHistory = conversationHistory;
            LastSystemPrompt = systemPrompt;
            LastEnableTools = enableTools;
            yield return "chunk1";
            yield return "chunk2";
            await Task.CompletedTask;
        }

        public Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> SummarizeTextAsync(string text, int maxLength = 500, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> ExtractDataAsync(string text, string schema, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> CompareDocumentsAsync(string sourceText, string targetText, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> AnswerQuestionAsync(string question, string context, CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task InvokeAsync_AlwaysPassesEnableToolsTrue()
    {
        var fake = new FakeAiService();
        var runtime = new AgentRuntime(fake);

        var result = await runtime.InvokeAsync("hello", "history", "system prompt");

        Assert.Equal("invoke-result", result);
        Assert.True(fake.LastEnableTools);
        Assert.Equal("hello", fake.LastMessage);
        Assert.Equal("history", fake.LastHistory);
        Assert.Equal("system prompt", fake.LastSystemPrompt);
    }

    [Fact]
    public async Task StreamAsync_AlwaysPassesEnableToolsTrue_AndYieldsAllChunks()
    {
        var fake = new FakeAiService();
        var runtime = new AgentRuntime(fake);

        var chunks = new List<string>();
        await foreach (var chunk in runtime.StreamAsync("hello"))
            chunks.Add(chunk);

        Assert.Equal(["chunk1", "chunk2"], chunks);
        Assert.True(fake.LastEnableTools);
    }
}
