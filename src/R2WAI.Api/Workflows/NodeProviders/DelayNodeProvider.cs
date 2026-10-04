using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Models;

namespace R2WAI.Api.Workflows.NodeProviders;

public sealed class DelayNodeProvider : INodeProvider
{
    public string StepType => "Delay";

    public IActivity CreateActivity(NodeCreationContext context) =>
        new Delay(new Input<TimeSpan>(ComputeDelay(context.Config)))
        {
            Name = context.Step.Name
        };

    // Exposed for WorkflowBridge's own delay-scheduling (see ResumeWorkflowAsync's doc comment) —
    // Delay steps never actually run as a real Elsa activity, so this is the only place that duration
    // math lives.
    internal static TimeSpan ComputeDelay(StepConfigDto? config)
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
}
