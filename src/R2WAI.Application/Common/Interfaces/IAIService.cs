namespace R2WAI.Application.Common.Interfaces;

public interface IAIService
{
    /// <param name="modelConfig">
    /// An already-resolved tenant-scoped model configuration to route this call through, produced by
    /// <see cref="IModelConfigurationResolver"/> at the caller. Null falls back to the process-wide
    /// "AI:Provider" configuration.
    /// </param>
    /// <param name="maxTokens">
    /// Explicit per-call override (e.g. a workflow step's own configured value). Takes priority over
    /// <paramref name="modelConfig"/>'s MaxTokens, which takes priority over the built-in default.
    /// </param>
    /// <param name="temperature">Same override precedence as <paramref name="maxTokens"/>.</param>
    Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default);
    /// <summary>
    /// <see cref="GenerateResponseAsync"/> for a short structured answer (a JSON array, a label) where a
    /// reasoning model's thinking pass only costs time: asks the provider to answer directly where it
    /// has a way to. Best-effort — a provider without one answers as <see cref="GenerateResponseAsync"/>
    /// would, so the caller must still tolerate a reasoning block in the reply. Never use it for chat.
    /// </summary>
    /// <param name="replyJsonSchema">
    /// The JSON schema the reply must follow, if it is JSON. A provider that can constrain decoding to
    /// it may, which also stops a model that always reasons from reasoning in its answer. Not a
    /// validation: the caller still parses the reply defensively.
    /// </param>
    Task<string> GenerateResponseWithoutReasoningAsync(string prompt, string? replyJsonSchema = null, ResolvedModelConfig? modelConfig = null, int? maxTokens = null, double? temperature = null, CancellationToken ct = default)
        => GenerateResponseAsync(prompt, modelConfig: modelConfig, maxTokens: maxTokens, temperature: temperature, ct: ct);
    /// <param name="modelConfig">Routes the call through this tenant-scoped model instead of the process-wide default. See <see cref="GenerateResponseAsync"/>.</param>
    Task<string> SummarizeTextAsync(string text, int maxLength = 500, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default);
    /// <param name="modelConfig">Routes the call through this tenant-scoped model instead of the process-wide default. See <see cref="GenerateResponseAsync"/>.</param>
    Task<string> ExtractDataAsync(string text, string schema, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default);
    /// <param name="modelConfig">Routes the call through this tenant-scoped model instead of the process-wide default. See <see cref="GenerateResponseAsync"/>.</param>
    Task<string> CompareDocumentsAsync(string sourceText, string targetText, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default);
    /// <param name="modelConfig">
    /// An already-resolved tenant-scoped model configuration to route this chat through (e.g. an
    /// assistant's linked <c>ModelConfiguration</c>), produced by <see cref="IModelConfigurationResolver"/>
    /// at the caller. Null falls back to the process-wide "AI:Provider" configuration, byte-identical
    /// to the pre-existing behavior.
    /// </param>
    /// <param name="enabledToolIds">
    /// The calling assistant's selected capability IDs (its <c>Tools</c> field, parsed). Null means
    /// "not configured" and attaches every tool the tenant has, byte-identical to pre-existing
    /// behavior — an empty (non-null) collection means "explicitly configured to zero tools" and
    /// attaches none. Ignored when <paramref name="enableTools"/> is false.
    /// </param>
    /// <param name="delegatableAssistantIds">
    /// The calling assistant's own allow-list of other assistants it may delegate a task to via
    /// AgentDelegationPlugin. Unlike <paramref name="enabledToolIds"/>, null/empty means NO
    /// delegation is allowed — there is no "every assistant" default to preserve for a capability
    /// this new. Ignored when <paramref name="enableTools"/> is false.
    /// </param>
    Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, IReadOnlyCollection<Guid>? delegatableAssistantIds = null, CancellationToken ct = default);
    /// <param name="enabledToolIds">See <see cref="ChatAsync"/>.</param>
    /// <param name="delegatableAssistantIds">See <see cref="ChatAsync"/>.</param>
    IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, bool enableTools = false, ResolvedModelConfig? modelConfig = null, IReadOnlyCollection<Guid>? enabledToolIds = null, IReadOnlyCollection<Guid>? delegatableAssistantIds = null, CancellationToken ct = default);
    Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default);
    /// <param name="modelConfig">Routes the call through this tenant-scoped model instead of the process-wide default. See <see cref="GenerateResponseAsync"/>.</param>
    Task<string> AnswerQuestionAsync(string question, string context, ResolvedModelConfig? modelConfig = null, CancellationToken ct = default);
}
