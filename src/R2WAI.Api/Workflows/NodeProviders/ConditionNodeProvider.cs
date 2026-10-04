using System.Text.Json;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

namespace R2WAI.Api.Workflows.NodeProviders;

public sealed class ConditionNodeProvider : INodeProvider
{
    public string StepType => "Condition";

    public IActivity CreateActivity(NodeCreationContext context)
    {
        var expression = context.Config?.ConditionExpression;
        var data = context.Data;
        return new If(() => EvaluateSimpleCondition(expression, data))
        {
            Name = context.Step.Name,
            Then = new WriteLine($"Condition '{expression}' was true") { Name = $"{context.Step.Name} (True)" },
            Else = new WriteLine($"Condition '{expression}' was false") { Name = $"{context.Step.Name} (False)" }
        };
    }

    private static bool EvaluateSimpleCondition(string? expression, string? data)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return true;

        string[] operators = [">=", "<=", "==", "!=", ">", "<"];
        foreach (var op in operators)
        {
            var idx = expression.IndexOf(op, StringComparison.Ordinal);
            if (idx <= 0)
                continue;

            var path = expression[..idx].Trim();
            var rightRaw = expression[(idx + op.Length)..].Trim().Trim('\'', '"');
            var leftRaw = ExtractJsonPath(data, path);

            if (leftRaw is null)
                return true;

            if (op is "==" or "!=")
            {
                var equal = string.Equals(leftRaw, rightRaw, StringComparison.OrdinalIgnoreCase);
                return op == "==" ? equal : !equal;
            }

            if (!double.TryParse(leftRaw, out var leftNum) || !double.TryParse(rightRaw, out var rightNum))
                return true;

            return op switch
            {
                ">" => leftNum > rightNum,
                "<" => leftNum < rightNum,
                ">=" => leftNum >= rightNum,
                "<=" => leftNum <= rightNum,
                _ => true
            };
        }

        return true;
    }

    private static string? ExtractJsonPath(string? json, string path)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var current = document.RootElement;
            foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!current.TryGetProperty(segment, out current))
                    return null;
            }

            return current.ValueKind == JsonValueKind.String ? current.GetString() : current.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
