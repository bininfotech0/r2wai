using Elsa.Workflows;
using Elsa.Workflows.Activities;

namespace R2WAI.Api.Workflows.NodeProviders;

/// <summary>
/// The generic fallback step type — logs what would have run. Also what StepActivityFactory falls
/// back to for any typeKey with no matching provider (see StepActivityFactory.CreateActivityForStep).
/// </summary>
public sealed class ActionNodeProvider : INodeProvider
{
    public string StepType => "Action";

    public IActivity CreateActivity(NodeCreationContext context) =>
        new WriteLine($"Step: {context.Step.Name} | Action: {context.Step.Action ?? "none"} | Role: {context.Step.AssignedRole ?? "System"}")
        {
            Name = context.Step.Name
        };
}
