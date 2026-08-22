using System.Text.Json;

namespace R2WAI.Api.Services;

public sealed record WorkflowDraftCondition(string Field, string Operator, string Value);

public sealed record WorkflowDraft(string? Name, string? Trigger, IReadOnlyList<string> Actions, IReadOnlyList<WorkflowDraftCondition> Conditions);

/// <summary>
/// Pure parsing/validation for the New Automation wizard's "what do you want to automate?" step.
/// Strips a model's markdown code fences, parses its JSON, and — critically — never trusts the
/// model's trigger/action labels verbatim: only values that exactly match (case-insensitive) the
/// fixed vocabulary the wizard actually offers are passed through. Kept free of IAIService/HTTP
/// types so it's directly unit-testable against arbitrary (including malformed or adversarial)
/// model output.
/// </summary>
public static class WorkflowDraftParser
{
    private static readonly WorkflowDraft Empty = new(null, null, [], []);

    public static WorkflowDraft Parse(string? rawModelOutput, IReadOnlyCollection<string> knownTriggers, IReadOnlyCollection<string> knownActions)
    {
        if (string.IsNullOrWhiteSpace(rawModelOutput))
            return Empty;

        var json = StripCodeFences(rawModelOutput);

        RawDraft? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<RawDraft>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return Empty;
        }

        if (parsed is null)
            return Empty;

        var trigger = knownTriggers.FirstOrDefault(t => string.Equals(t, parsed.Trigger, StringComparison.OrdinalIgnoreCase));

        var actions = (parsed.Actions ?? [])
            .Select(a => knownActions.FirstOrDefault(k => string.Equals(k, a, StringComparison.OrdinalIgnoreCase)))
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        var conditions = (parsed.Conditions ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Field))
            .Select(c => new WorkflowDraftCondition(c.Field!, string.IsNullOrWhiteSpace(c.Operator) ? "Is equal to" : c.Operator!, c.Value ?? string.Empty))
            .ToList();

        return new WorkflowDraft(parsed.Name, trigger, actions, conditions);
    }

    private static string StripCodeFences(string text)
    {
        var json = text.Trim();
        if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) json = json[7..];
        else if (json.StartsWith("```")) json = json[3..];
        if (json.EndsWith("```")) json = json[..^3];
        return json.Trim();
    }

    private sealed class RawDraft
    {
        public string? Name { get; set; }
        public string? Trigger { get; set; }
        public string[]? Actions { get; set; }
        public RawCondition[]? Conditions { get; set; }
    }

    private sealed class RawCondition
    {
        public string? Field { get; set; }
        public string? Operator { get; set; }
        public string? Value { get; set; }
    }
}
