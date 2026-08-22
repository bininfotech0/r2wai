using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Infrastructure.AI.ModelGateway;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers the IModelGateway/IModelProvider extraction from SemanticKernelService.CreateKernel().
/// These assert the gateway still selects and configures the same connector per provider as the
/// inline branching it replaced — a refactor, not a behavior change.
/// </summary>
public class ModelGatewayTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ModelGateway BuildGateway(IConfiguration configuration) =>
        new(configuration, NullLogger<ModelGateway>.Instance,
        [
            new OpenAiModelProvider(configuration, NullLogger<OpenAiModelProvider>.Instance),
            new OllamaModelProvider(configuration, NullLogger<OllamaModelProvider>.Instance),
            new ZaiModelProvider(configuration, NullLogger<ZaiModelProvider>.Instance),
        ]);

    [Fact]
    public void ActiveProviderName_DefaultsToOpenAi_WhenUnconfigured()
    {
        var gateway = BuildGateway(Config([]));
        Assert.Equal("openai", gateway.ActiveProviderName);
    }

    [Fact]
    public void ActiveProviderName_IsCaseInsensitive()
    {
        var gateway = BuildGateway(Config(new() { ["AI:Provider"] = "OLLAMA" }));
        Assert.Equal("ollama", gateway.ActiveProviderName);
    }

    [Fact]
    public void ConfigureKernel_OpenAi_WithApiKey_RegistersChatAndEmbeddingServices()
    {
        var gateway = BuildGateway(Config(new()
        {
            ["AI:Provider"] = "openai",
            ["AI:OpenAI:ApiKey"] = "test-key",
        }));

        var builder = Kernel.CreateBuilder();
        gateway.ConfigureKernel(builder);
        var kernel = builder.Build();

        Assert.NotNull(kernel.Services.GetService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());
        Assert.NotNull(kernel.Services.GetService<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>());
    }

    [Fact]
    public void ConfigureKernel_OpenAi_NoApiKey_RegistersNoChatService()
    {
        // Matches pre-refactor behavior: a missing API key logs a warning and leaves the kernel
        // without a chat completion service, rather than throwing — callers (ChatAsync etc.)
        // check for this and return a friendly "not configured" message.
        var gateway = BuildGateway(Config([]));

        var builder = Kernel.CreateBuilder();
        gateway.ConfigureKernel(builder);
        var kernel = builder.Build();

        Assert.Null(kernel.Services.GetService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());
    }

    [Fact]
    public void ConfigureKernel_Ollama_WithEndpoint_RegistersChatAndEmbeddingServices()
    {
        var gateway = BuildGateway(Config(new()
        {
            ["AI:Provider"] = "ollama",
            ["AI:Ollama:Endpoint"] = "http://localhost:11434",
        }));

        var builder = Kernel.CreateBuilder();
        gateway.ConfigureKernel(builder);
        var kernel = builder.Build();

        Assert.NotNull(kernel.Services.GetService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());
        Assert.NotNull(kernel.Services.GetService<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>());
    }

    [Fact]
    public void ConfigureKernel_Ollama_MissingEndpoint_Throws()
    {
        var gateway = BuildGateway(Config(new() { ["AI:Provider"] = "ollama" }));
        var builder = Kernel.CreateBuilder();

        Assert.Throws<ConfigurationException>(() => gateway.ConfigureKernel(builder));
    }

    [Fact]
    public void ConfigureKernel_Zai_WithApiKey_RegistersChatServiceOnly()
    {
        var gateway = BuildGateway(Config(new()
        {
            ["AI:Provider"] = "zai",
            ["AI:ZAI:ApiKey"] = "test-key",
        }));

        var builder = Kernel.CreateBuilder();
        gateway.ConfigureKernel(builder);
        var kernel = builder.Build();

        Assert.NotNull(kernel.Services.GetService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());
        // Z.ai is chat/reasoning only — no embedding model is configured for it.
        Assert.Null(kernel.Services.GetService<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>());
    }

    [Fact]
    public void ConfigureKernel_Zai_MissingApiKey_Throws()
    {
        var gateway = BuildGateway(Config(new() { ["AI:Provider"] = "zai" }));
        var builder = Kernel.CreateBuilder();

        Assert.Throws<ConfigurationException>(() => gateway.ConfigureKernel(builder));
    }

    [Fact]
    public void ConfigureKernel_UnrecognizedProvider_FallsBackToOpenAi()
    {
        var gateway = BuildGateway(Config(new()
        {
            ["AI:Provider"] = "not-a-real-provider",
            ["AI:OpenAI:ApiKey"] = "test-key",
        }));

        var builder = Kernel.CreateBuilder();
        gateway.ConfigureKernel(builder);
        var kernel = builder.Build();

        Assert.NotNull(kernel.Services.GetService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>());
    }
}
