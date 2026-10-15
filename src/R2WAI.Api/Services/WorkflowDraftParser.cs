using System.Text.Json;

namespace R2WAI.Api.Services;

public sealed record WorkflowDraftCondition(string Field, string Operator, string Value);

public sealed record WorkflowDraftStep(string Name, string Type);

// Actions = the step types in order (kept for older callers); Steps adds the model's own step names.
public sealed record WorkflowDraft(string? Name, string? Trigger, IReadOnlyList<string> Actions, IReadOnlyList<WorkflowDraftCondition> Conditions)
{
    public IReadOnlyList<WorkflowDraftStep> Steps { get; init; } = [];
}

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

        // "steps": [{type, name}] is the current shape; a bare "actions": ["<type>"] list is still
        // accepted. Either way only exact (case-insensitive) vocabulary matches survive.
        IEnumerable<(string? Type, string? Name)> rawSteps = parsed.Steps is { Length: > 0 }
            ? parsed.Steps.Select(s => (s.Type, s.Name))
            : (parsed.Actions ?? []).Select(a => ((string?)a, (string?)null));

        var steps = new List<WorkflowDraftStep>();
        foreach (var (rawType, rawName) in rawSteps)
        {
            var type = knownActions.FirstOrDefault(k => string.Equals(k, rawType, StringComparison.OrdinalIgnoreCase));
            if (type is null)
                continue;
            var name = rawName?.Trim();
            steps.Add(new WorkflowDraftStep(string.IsNullOrEmpty(name) ? type : name[..Math.Min(name.Length, 100)], type));
        }
        var actions = steps.Select(s => s.Type).ToList();

        var conditions = (parsed.Conditions ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Field))
            .Select(c => new WorkflowDraftCondition(c.Field!, string.IsNullOrWhiteSpace(c.Operator) ? "Is equal to" : c.Operator!, c.Value ?? string.Empty))
            .ToList();

        return new WorkflowDraft(parsed.Name, trigger, actions, conditions) { Steps = steps };
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
        public RawStep[]? Steps { get; set; }
        public RawCondition[]? Conditions { get; set; }
    }

    private sealed class RawStep
    {
        public string? Type { get; set; }
        public string? Name { get; set; }
    }

    private sealed class RawCondition
    {
        public string? Field { get; set; }
        public string? Operator { get; set; }
        public string? Value { get; set; }
    }
}
