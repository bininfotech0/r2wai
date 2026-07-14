using System.Text.Json;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace R2WAI.Api.Workflows;

[Activity("R2WAI", "Transform", "Extracts or reformats data between workflow variables.")]
public class TransformStepActivity : Activity<string>
{
    [Input(Description = "The input value to transform (a JSON document or plain string).")]
    public Input<string?> Value { get; set; } = default!;

    [Input(Description = "The transform operation: extract, format, or passthrough.")]
    public Input<string?> Operation { get; set; } = default!;

    [Input(Description = "For 'extract', a dot-separated JSON property path. For 'format', a composite format string.")]
    public Input<string?> Expression { get; set; } = default!;

    [Input(Description = "The name of the workflow variable to write the result to.")]
    public Input<string?> OutputVariableName { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var value = context.Get(Value) ?? string.Empty;
        var operation = (context.Get(Operation) ?? "extract").Trim().ToLowerInvariant();
        var expression = context.Get(Expression);
        var outputName = context.Get(OutputVariableName);

        var result = operation switch
        {
            "extract" => ExtractJsonPath(value, expression) ?? value,
            "format" => string.IsNullOrEmpty(expression) ? value : string.Format(expression, value),
            _ => value
        };

        if (!string.IsNullOrWhiteSpace(outputName))
            context.SetVariable(outputName, result);

        context.Set(Result, result);
        await context.CompleteActivityAsync();
    }

    private static string? ExtractJsonPath(string json, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return json;

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
