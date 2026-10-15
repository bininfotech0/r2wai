using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.AI.ModelGateway;

/// <summary>
/// One AI provider strategy (OpenAI, Ollama, Z.ai, ...). Configures an <see cref="IKernelBuilder"/>
/// with that provider's chat/embedding connectors, client options, and timeouts.
/// </summary>
public interface IModelProvider
{
    /// <summary>Matches the "AI:Provider" configuration value (case-insensitive), e.g. "openai".</summary>
    string Name { get; }

    /// <param name="config">
    /// A resolved tenant/assistant-scoped model config to use instead of this provider's own
    /// "AI:{Provider}:*" configuration values. Any field left null on a non-null config falls back
    /// to that same configuration read, field by field — so a partially-specified ModelConfiguration
    /// (e.g. just a different ModelId) still inherits the rest from the process-wide settings. Null
    /// means "use configuration entirely," byte-identical to the pre-existing behavior.
    /// </param>
    void Configure(IKernelBuilder builder, ResolvedModelConfig? config = null);

    /// <summary>
    /// Adjusts one request so a reasoning model answers directly instead of thinking first, for
    /// <see cref="IAIService.GenerateResponseWithoutReasoningAsync"/> only, never for chat. A provider
    /// with no way to switch reasoning off leaves the request as it is: that is the default here.
    /// </summary>
    /// <param name="replyJsonSchema">The reply's JSON schema, when the caller has one; see the IAIService method.</param>
    void DisableReasoning(OpenAIPromptExecutionSettings settings, string? replyJsonSchema) { }
}
