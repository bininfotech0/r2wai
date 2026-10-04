using Elsa.Workflows;
using Elsa.Workflows.Models;
using R2WAI.Api.Workflows;

namespace R2WAI.Api.Workflows.NodeProviders;

public sealed class TransformNodeProvider : INodeProvider
{
    public string StepType => "Transform";

    public IActivity CreateActivity(NodeCreationContext context)
    {
        var config = context.Config;
        return new TransformStepActivity
        {
            Name = context.Step.Name,
            Value = new Input<string?>(config?.TransformInput ?? context.Data),
            Operation = new Input<string?>(config?.TransformOperation ?? "extract"),
            Expression = new Input<string?>(config?.TransformExpression),
            OutputVariableName = new Input<string?>(config?.TransformOutput)
        };
    }
}
