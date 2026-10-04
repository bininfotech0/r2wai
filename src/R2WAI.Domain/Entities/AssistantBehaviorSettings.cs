namespace R2WAI.Domain.Entities;

/// <summary>
/// Mirrors R2WAI.Client's BehaviorSettings shape (the Assistant editor's Behavior tab) — parsed from
/// AssistantDefinition.Settings. Deliberately plain data with no external dependency, same rationale
/// as ResolvedModelConfig: consumed all the way up in the Api/Application layers.
/// </summary>
public sealed class AssistantBehaviorSettings
{
    public string? ResponseStyle { get; init; }
    public string? AnswerLength { get; init; }
    public bool? CitationsEnabled { get; init; }
    public bool? AskClarification { get; init; }
    public double? Temperature { get; init; }
    public int? MaxOutputTokens { get; init; }
    /// <summary>"Standard" (default/null) or "Agentic" — R2WAI 2.0 §6. Opt-in: null/anything else
    /// keeps today's single-pass retrieval byte-identical. See IAgenticRetrievalOrchestrator.</summary>
    public string? RetrievalMode { get; init; }

    /// <summary>
    /// Translates the qualitative knobs (ResponseStyle/AnswerLength/AskClarification) into a short
    /// natural-language instruction appended to the assistant's system prompt — the only lever
    /// available for behavior that isn't a real model parameter. Null when nothing here would change
    /// anything, so callers can skip appending an empty instruction.
    /// </summary>
    public string? BuildPromptAddendum()
    {
        var parts = new List<string>();

        switch (ResponseStyle)
        {
            case "Concise": parts.Add("Keep your responses concise and to the point."); break;
            case "Detailed": parts.Add("Provide thorough, detailed responses with explanations."); break;
            case "Friendly": parts.Add("Respond in a warm, friendly, conversational tone."); break;
            case "Professional": parts.Add("Respond in a formal, professional tone."); break;
        }

        switch (AnswerLength)
        {
            case "Brief": parts.Add("Answer in 1-2 sentences whenever possible."); break;
            case "Thorough": parts.Add("Give complete, thorough answers rather than the shortest possible response."); break;
        }

        if (AskClarification == true)
            parts.Add("If the user's request is ambiguous or missing key details, ask a clarifying question before proceeding.");

        return parts.Count > 0 ? string.Join(" ", parts) : null;
    }
}
