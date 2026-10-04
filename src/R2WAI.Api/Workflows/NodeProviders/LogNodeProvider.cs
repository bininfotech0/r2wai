using Elsa.Workflows;
using Elsa.Workflows.Activities;

namespace R2WAI.Api.Workflows.NodeProviders;

/// <summary>
/// A trivial new step type added purely to prove Track B Phase 5's registry actually decouples node
/// construction from StepActivityFactory — this class plus its DI registration (Program.cs) are the
/// only things this node needed; CreateActivityForStep itself was not touched.
/// </summary>
public sealed class LogNodeProvider : INodeProvider
{
    public string StepType => "Log";

    public IActivity CreateActivity(NodeCreationContext context) =>
        new WriteLine($"[Log] {context.Step.Name}: {context.Data ?? "(no data)"}")
        {
            Name = context.Step.Name
        };
}
