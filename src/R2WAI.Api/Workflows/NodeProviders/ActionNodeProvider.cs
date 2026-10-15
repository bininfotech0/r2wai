using Elsa.Workflows;
using Elsa.Workflows.Activities;

namespace R2WAI.Api.Workflows.NodeProviders;

/// <summary>
/// The placeholder step type — performs no action, only logs that it ran. Also what
/// StepActivityFactory falls back to for any typeKey with no matching provider. Publishing is blocked
/// while a workflow contains one (WorkflowsController.GetPublishBlockers); it still runs in test runs
/// so older automations can be opened and fixed.
/// </summary>
public sealed class ActionNodeProvider : INodeProvider
{
    public string StepType => "Action";

    public IActivity CreateActivity(NodeCreationContext context) =>
        new WriteLine($"Placeholder step '{context.Step.Name}' ran but performed no action — replace it with a real step type.")
        {
            Name = context.Step.Name
        };
}
