using Microsoft.SemanticKernel;

namespace R2WAI.Infrastructure.AI.ModelGateway;

/// <summary>
/// Selects the active AI provider (per "AI:Provider" configuration: openai/ollama/zai/...) and
/// configures a kernel builder against it. This is the single seam assistants go through to reach
/// a model — nothing else should read "AI:Provider" or construct an OpenAIClient directly, so
/// swapping/adding providers or enforcing an approved-model policy only touches this class.
/// </summary>
public interface IModelGateway
{
    /// <summary>The provider name currently selected by configuration.</summary>
    string ActiveProviderName { get; }

    void ConfigureKernel(IKernelBuilder builder);
}
