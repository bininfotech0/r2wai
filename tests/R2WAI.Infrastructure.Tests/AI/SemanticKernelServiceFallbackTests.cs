using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.AI.ModelGateway;
using System.Runtime.CompilerServices;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers SemanticKernelService's cross-provider fallback: ChatAsync/StreamChatAsync retry once
/// against "AI:FallbackProvider" (surfaced via IModelGateway.FallbackProviderName) when the
/// primary provider either has no chat completion service configured at all, or fails at runtime
/// -- but only before any streamed content has already reached the caller, and never when no
/// fallback is configured or the fallback provider itself can't be initialized (in which case the
/// ORIGINAL failure must propagate, not the fallback's own error).
///
/// Each test uses its own unique provider-name strings so SemanticKernelService's process-wide
/// static kernel cache (keyed by provider/model/endpoint/key) can't leak a kernel built by one
/// test into another.
/// </summary>
public class SemanticKernelServiceFallbackTests
{
    private static SemanticKernelService BuildService(FakeModelGateway gateway) =>
        new(new ConfigurationBuilder().Build(), NullLogger<SemanticKernelService>.Instance,
            new ServiceCollection().BuildServiceProvider(), gateway);

    private static ResolvedModelConfig PrimaryConfig(string provider) =>
        new(provider, null, null, null, null, null, null);

    [Fact]
    public async Task ChatAsync_PrimaryThrows_NoFallbackConfigured_RethrowsOriginal()
    {
        const string primary = "primary-chat-nofallback";
        var gateway = new FakeModelGateway { ActiveProviderName = primary };
        gateway.Register(primary, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.Throws(new InvalidOperationException("primary down"))));
        var sut = BuildService(gateway);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ChatAsync("hi", modelConfig: PrimaryConfig(primary)));
        Assert.Equal("primary down", ex.Message);
    }

    [Fact]
    public async Task ChatAsync_PrimaryThrows_FallbackConfigured_ReturnsFallbackReply()
    {
        const string primary = "primary-chat-fb-works";
        const string fallback = "fallback-chat-fb-works";
        var gateway = new FakeModelGateway { ActiveProviderName = primary, FallbackProviderName = fallback };
        gateway.Register(primary, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.Throws(new InvalidOperationException("primary down"))));
        gateway.Register(fallback, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.Succeeds("fallback reply")));
        var sut = BuildService(gateway);

        var result = await sut.ChatAsync("hi", modelConfig: PrimaryConfig(primary));

        Assert.Equal("fallback reply", result);
    }

    [Fact]
    public async Task ChatAsync_PrimaryNotConfigured_FallbackConfigured_ReturnsFallbackReply()
    {
        const string primary = "primary-chat-unconfigured";
        const string fallback = "fallback-chat-unconfigured";
        var gateway = new FakeModelGateway { ActiveProviderName = primary, FallbackProviderName = fallback };
        // No configurer registered for `primary` at all -- matches a real provider with no API
        // key/endpoint set, which leaves the kernel with no IChatCompletionService.
        gateway.Register(fallback, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.Succeeds("fallback reply")));
        var sut = BuildService(gateway);

        var result = await sut.ChatAsync("hi", modelConfig: PrimaryConfig(primary));

        Assert.Equal("fallback reply", result);
    }

    [Fact]
    public async Task ChatAsync_PrimaryNotConfigured_NoFallback_ReturnsNotConfiguredMessage()
    {
        const string primary = "primary-chat-unconfigured-nofallback";
        var gateway = new FakeModelGateway { ActiveProviderName = primary };
        var sut = BuildService(gateway);

        var result = await sut.ChatAsync("hi", modelConfig: PrimaryConfig(primary));

        Assert.Contains("not configured", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatAsync_FallbackSameAsPrimary_NotAttempted_RethrowsOriginal()
    {
        const string primary = "primary-chat-samefallback";
        var gateway = new FakeModelGateway { ActiveProviderName = primary, FallbackProviderName = primary };
        gateway.Register(primary, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.Throws(new InvalidOperationException("primary down"))));
        var sut = BuildService(gateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ChatAsync("hi", modelConfig: PrimaryConfig(primary)));
    }

    [Fact]
    public async Task ChatAsync_FallbackMisconfigured_RethrowsOriginalNotFallbackError()
    {
        const string primary = "primary-chat-brokenfallback";
        const string fallback = "fallback-chat-brokenfallback";
        var gateway = new FakeModelGateway { ActiveProviderName = primary, FallbackProviderName = fallback };
        gateway.Register(primary, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.Throws(new InvalidOperationException("primary down"))));
        gateway.Register(fallback, _ => throw new ConfigurationException("fallback missing endpoint"));
        var sut = BuildService(gateway);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ChatAsync("hi", modelConfig: PrimaryConfig(primary)));
        Assert.Equal("primary down", ex.Message);
    }

    [Fact]
    public async Task StreamChatAsync_PrimaryThrowsBeforeFirstChunk_FallbackConfigured_StreamsFallbackContent()
    {
        const string primary = "primary-stream-fb-works";
        const string fallback = "fallback-stream-fb-works";
        var gateway = new FakeModelGateway { ActiveProviderName = primary, FallbackProviderName = fallback };
        gateway.Register(primary, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.Throws(new InvalidOperationException("primary down"))));
        gateway.Register(fallback, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.Succeeds("fallback chunk")));
        var sut = BuildService(gateway);

        var chunks = new List<string>();
        await foreach (var chunk in sut.StreamChatAsync("hi", modelConfig: PrimaryConfig(primary)))
            chunks.Add(chunk);

        Assert.Equal(["fallback chunk"], chunks);
    }

    [Fact]
    public async Task StreamChatAsync_PrimaryThrows_NoFallback_RethrowsOriginal()
    {
        const string primary = "primary-stream-nofallback";
        var gateway = new FakeModelGateway { ActiveProviderName = primary };
        gateway.Register(primary, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.Throws(new InvalidOperationException("primary down"))));
        var sut = BuildService(gateway);

        async Task Act()
        {
            await foreach (var _ in sut.StreamChatAsync("hi", modelConfig: PrimaryConfig(primary))) { }
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Equal("primary down", ex.Message);
    }

    [Fact]
    public async Task StreamChatAsync_PrimaryYieldsThenThrows_FallbackNotAttempted_RethrowsOriginal()
    {
        const string primary = "primary-stream-midfail";
        const string fallback = "fallback-stream-midfail";
        var gateway = new FakeModelGateway { ActiveProviderName = primary, FallbackProviderName = fallback };
        gateway.Register(primary, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.StreamsThenThrows("first chunk", new InvalidOperationException("dropped mid-stream"))));
        gateway.Register(fallback, b => b.Services.AddSingleton<IChatCompletionService>(
            FakeChatCompletionService.Succeeds("fallback chunk")));
        var sut = BuildService(gateway);

        var chunks = new List<string>();
        async Task Act()
        {
            await foreach (var chunk in sut.StreamChatAsync("hi", modelConfig: PrimaryConfig(primary)))
                chunks.Add(chunk);
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Equal("dropped mid-stream", ex.Message);
        Assert.Equal(["first chunk"], chunks);
    }
}

sealed class FakeModelGateway : IModelGateway
{
    private readonly Dictionary<string, Action<IKernelBuilder>> _configurers = new(StringComparer.OrdinalIgnoreCase);

    public string ActiveProviderName { get; set; } = "primary";
    public string? FallbackProviderName { get; set; }

    public void Register(string provider, Action<IKernelBuilder> configure) => _configurers[provider] = configure;

    public void ConfigureKernel(IKernelBuilder builder, ResolvedModelConfig? config = null)
    {
        var providerName = config?.Provider ?? ActiveProviderName;
        if (_configurers.TryGetValue(providerName, out var configure))
            configure(builder);
    }
}

sealed class FakeChatCompletionService : IChatCompletionService
{
    private readonly string? _reply;
    private readonly Exception? _failImmediately;
    private readonly (string Chunk, Exception Then)? _streamThenThrow;

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public static FakeChatCompletionService Succeeds(string reply) => new(reply, null, null);
    public static FakeChatCompletionService Throws(Exception ex) => new(null, ex, null);
    public static FakeChatCompletionService StreamsThenThrows(string chunk, Exception ex) => new(null, null, (chunk, ex));

    private FakeChatCompletionService(string? reply, Exception? failImmediately, (string, Exception)? streamThenThrow)
    {
        _reply = reply;
        _failImmediately = failImmediately;
        _streamThenThrow = streamThenThrow;
    }

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        if (_failImmediately is not null)
            throw _failImmediately;
        return Task.FromResult<IReadOnlyList<ChatMessageContent>>([new ChatMessageContent(AuthorRole.Assistant, _reply)]);
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_failImmediately is not null)
            throw _failImmediately;

        if (_streamThenThrow is { } streamThenThrow)
        {
            await Task.Yield();
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, streamThenThrow.Chunk);
            throw streamThenThrow.Then;
        }

        await Task.Yield();
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, _reply);
    }
}
