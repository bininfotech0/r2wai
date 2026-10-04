using Microsoft.SemanticKernel;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.AI.ModelGateway;

/// <summary>
/// Selects the AI provider — either from a resolved tenant/assistant-scoped <see cref="ResolvedModelConfig"/>,
/// or (when none is given) the process-wide "AI:Provider" configuration — and configures a kernel
/// builder against it. This is the single seam assistants go through to reach a model — nothing
/// else should read "AI:Provider" or construct an OpenAIClient directly, so swapping/adding
/// providers or enforcing an approved-model policy only touches this class.
/// </summary>
public interface IModelGateway
{
    /// <summary>The provider name currently selected by configuration.</summary>
    string ActiveProviderName { get; }

    /// <summary>
    /// Optional secondary provider name ("AI:FallbackProvider") that <c>SemanticKernelService</c>
    /// retries against once when the primary provider fails at runtime or has no chat completion
    /// service configured at all. Null when unset — no fallback is attempted, byte-identical to
    /// the pre-existing behavior.
    /// </summary>
    string? FallbackProviderName { get; }

    void ConfigureKernel(IKernelBuilder builder, ResolvedModelConfig? config = null);
}
