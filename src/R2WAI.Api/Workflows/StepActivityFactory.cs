using System.Text.Json;
using Elsa.Email.Activities;
using Elsa.Extensions;
using Elsa.Http;
using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;
using R2WAI.Application.Features.Workflows.DTOs;

namespace R2WAI.Api.Workflows;

/// <summary>
/// Builds the Elsa activity for a workflow step. Must be called at execution time (from
/// <see cref="StepTrackingActivity.ConfigureActivities"/>), not at workflow-definition build time --
/// Elsa persists Flowchart definitions as JSON before running them, and a Composite's dynamically
/// assigned Root does not round-trip its Input values through that serialization.
/// </summary>
public static class StepActivityFactory
{
    private static readonly JsonSerializerOptions StepConfigJsonOptions = new(JsonSerializerDefaults.Web);

    public static string ClassifyStepType(WorkflowStepDto step, ILogger? logger = null)
    {
        var typeKey = (step.Type ?? step.Action ?? string.Empty).Trim().ToLowerInvariant();
        return typeKey switch
        {
            "approval" => "Approval",
            "ai generate" => "AI Generate",
            "email" => "Email",
            "api call" => "API Call",
            "condition" => "Condition",
            "delay" => "Delay",
            "transform" => "Transform",
            "action" => "Action",
            _ => ClassifyStepTypeLegacy(step, logger)
        };
    }

    private static string ClassifyStepTypeLegacy(WorkflowStepDto step, ILogger? logger)
    {
        var legacy = (step.Name + " " + (step.Action ?? string.Empty)).ToLowerInvariant();
        var stepType = legacy.Contains("approval") ? "Approval" : legacy.Contains("ai") ? "AI Generate" : "Action";
        logger?.LogWarning(
            "Step '{Step}' used legacy free-text classification (resolved to '{StepType}'); consider re-saving the workflow",
            step.Name, stepType);
        return stepType;
    }

    public static IActivity CreateActivityForStep(
        WorkflowStepDto step,
        Guid workflowId,
        Guid tenantId,
        Guid userId,
        Guid instanceId,
        string? data,
        ILogger? logger = null)
    {
        var config = step.Config?.Deserialize<StepConfigDto>(StepConfigJsonOptions);
        var typeKey = ClassifyStepType(step, logger);

        switch (typeKey)
        {
            case "Approval":
                return new ApprovalStepActivity
                {
                    Name = step.Name,
                    TenantId = new Input<string>(tenantId.ToString()),
                    WorkflowInstanceId = new Input<string>(instanceId.ToString()),
                    WorkflowDefinitionId = new Input<string>(workflowId.ToString()),
                    RequesterId = new Input<string>(userId.ToString()),
                    Data = new Input<string?>(data)
                };

            case "AI Generate":
            {
                var prompt = !string.IsNullOrEmpty(config?.AiPrompt)
                    ? config.AiPrompt
                    : !string.IsNullOrEmpty(step.Action) ? step.Action : $"Execute step: {step.Name}";

                return new InvokeSemanticKernelActivity
                {
                    Name = step.Name,
                    Prompt = new Input<string>(prompt),
                    SystemPrompt = new Input<string?>(
                        $"You are executing workflow step '{step.Name}'. Assigned role: {step.AssignedRole ?? "System"}."),
                    MaxTokens = new Input<int>(config?.AiMaxTokens ?? 1024),
                    Temperature = new Input<double>(config?.AiTemperature ?? 0.7)
                };
            }

            case "Email":
                return new SendEmail
                {
                    Name = step.Name,
                    To = new Input<ICollection<string>>(SplitAddresses(config?.EmailTo)),
                    Cc = new Input<ICollection<string>>(SplitAddresses(config?.EmailCc)),
                    Subject = new Input<string?>(config?.EmailSubject ?? step.Name),
                    Body = new Input<string>(config?.EmailBody ?? string.Empty)
                };

            case "API Call":
                return new SendHttpRequest
                {
                    Name = step.Name,
                    Url = new Input<Uri?>(new Uri(config?.ApiUrl ?? "about:blank", UriKind.RelativeOrAbsolute)),
                    Method = new Input<string>((config?.ApiMethod ?? "GET").ToUpperInvariant()),
                    Content = string.IsNullOrEmpty(config?.ApiBody) ? null! : new Input<object?>(config.ApiBody),
                    RequestHeaders = new Input<HttpHeaders?>(ParseHeaders(config?.ApiHeaders))
                };

            case "Delay":
                return new Delay(new Input<TimeSpan>(ComputeDelay(config)))
                {
                    Name = step.Name
                };

            case "Condition":
            {
                var expression = config?.ConditionExpression;
                return new If(() => EvaluateSimpleCondition(expression, data))
                {
                    Name = step.Name,
                    Then = new WriteLine($"Condition '{expression}' was true") { Name = $"{step.Name} (True)" },
                    Else = new WriteLine($"Condition '{expression}' was false") { Name = $"{step.Name} (False)" }
                };
            }

            case "Transform":
                return new TransformStepActivity
                {
                    Name = step.Name,
                    Value = new Input<string?>(config?.TransformInput ?? data),
                    Operation = new Input<string?>(config?.TransformOperation ?? "extract"),
                    Expression = new Input<string?>(config?.TransformExpression),
                    OutputVariableName = new Input<string?>(config?.TransformOutput)
                };

            default:
                return new WriteLine($"Step: {step.Name} | Action: {step.Action ?? "none"} | Role: {step.AssignedRole ?? "System"}")
                {
                    Name = step.Name
                };
        }
    }

    private static ICollection<string> SplitAddresses(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static HttpHeaders ParseHeaders(string? rawHeadersJson)
    {
        if (string.IsNullOrWhiteSpace(rawHeadersJson))
            return new HttpHeaders();

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(rawHeadersJson) ?? [];
            var dict = parsed.ToDictionary(kv => kv.Key, kv => new[] { kv.Value });
            return new HttpHeaders(dict);
        }
        catch (JsonException)
        {
            return new HttpHeaders();
        }
    }

    private static TimeSpan ComputeDelay(StepConfigDto? config)
    {
        var duration = Math.Max(config?.DelayDuration ?? 1, 0);
        return (config?.DelayUnit ?? "hours").ToLowerInvariant() switch
        {
            "minutes" => TimeSpan.FromMinutes(duration),
            "hours" => TimeSpan.FromHours(duration),
            "days" => TimeSpan.FromDays(duration),
            _ => TimeSpan.FromHours(duration)
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

    private sealed class StepConfigDto
    {
        public string? EmailTo { get; set; }
        public string? EmailCc { get; set; }
        public string? EmailSubject { get; set; }
        public string? EmailBody { get; set; }
        public string? AiPrompt { get; set; }
        public int AiMaxTokens { get; set; } = 1024;
        public double AiTemperature { get; set; } = 0.7;
        public string? ApiMethod { get; set; } = "GET";
        public string? ApiUrl { get; set; }
        public string? ApiHeaders { get; set; }
        public string? ApiBody { get; set; }
        public string? ConditionExpression { get; set; }
        public int DelayDuration { get; set; } = 1;
        public string? DelayUnit { get; set; } = "hours";
        public string? TransformInput { get; set; }
        public string? TransformOperation { get; set; } = "extract";
        public string? TransformExpression { get; set; }
        public string? TransformOutput { get; set; }
        public List<string>? NextSteps { get; set; }
    }
}
