using System.Text.Json;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using R2WAI.Api.Workflows.NodeProviders;
using R2WAI.Application.Features.Workflows.DTOs;

namespace R2WAI.Api.Workflows;

/// <summary>
/// Builds the Elsa activity for a workflow step. Must be called at execution time (from
/// <see cref="StepTrackingActivity.ConfigureActivities"/>), not at workflow-definition build time --
/// Elsa persists Flowchart definitions as JSON before running them, and a Composite's dynamically
/// assigned Root does not round-trip its Input values through that serialization.
///
/// CreateActivityForStep is a registry lookup over DI-registered <see cref="INodeProvider"/>
/// implementations (see NodeProviders/), one per step type, keyed by the same strings
/// ClassifyStepType produces — not a hardcoded switch. Adding a new step type means adding a new
/// INodeProvider class + DI registration, no edit to this class (Track B Phase 5).
/// </summary>
public class StepActivityFactory
{
    private static readonly JsonSerializerOptions StepConfigJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyDictionary<string, INodeProvider> _providers;

    public StepActivityFactory(IEnumerable<INodeProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.StepType, StringComparer.Ordinal);
    }

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
            // Proves the Phase 5 registry actually decouples node construction from this switch:
            // LogNodeProvider was added purely as a new INodeProvider class + DI registration, with
            // zero edits to CreateActivityForStep itself. This one-line classification case is the
            // only touch this file needed — ClassifyStepType (turning free text into a canonical key)
            // is a separate concern from the registry (turning a key into an activity).
            "log" => "Log",
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

    public IActivity CreateActivityForStep(
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
        var context = new NodeCreationContext(step, workflowId, tenantId, userId, instanceId, data, config);

        if (_providers.TryGetValue(typeKey, out var provider))
            return provider.CreateActivity(context);

        // No provider registered for this key — same generic fallback the old hardcoded switch's
        // `default:` case used. In practice every key ClassifyStepType can produce today has a
        // matching provider (including "Action"), so this only guards a genuinely unmapped future key.
        return new WriteLine($"Step: {step.Name} | Action: {step.Action ?? "none"} | Role: {step.AssignedRole ?? "System"}")
        {
            Name = step.Name
        };
    }
}
