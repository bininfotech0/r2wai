using Microsoft.SemanticKernel;

namespace R2WAI.Infrastructure.AI.ModelGateway;

/// <summary>
/// One AI provider strategy (OpenAI, Ollama, Z.ai, ...). Configures an <see cref="IKernelBuilder"/>
/// with that provider's chat/embedding connectors, client options, and timeouts.
/// </summary>
public interface IModelProvider
{
    /// <summary>Matches the "AI:Provider" configuration value (case-insensitive), e.g. "openai".</summary>
    string Name { get; }

    void Configure(IKernelBuilder builder);
}
