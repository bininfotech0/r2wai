using Elsa.Workflows;
using Elsa.Workflows.Models;
using R2WAI.Api.Workflows;

namespace R2WAI.Api.Workflows.NodeProviders;

public sealed class ApprovalNodeProvider : INodeProvider
{
    public string StepType => "Approval";

    public IActivity CreateActivity(NodeCreationContext context) => new ApprovalStepActivity
    {
        Name = context.Step.Name,
        TenantId = new Input<string>(context.TenantId.ToString()),
        WorkflowInstanceId = new Input<string>(context.InstanceId.ToString()),
        WorkflowDefinitionId = new Input<string>(context.WorkflowId.ToString()),
        RequesterId = new Input<string>(context.UserId.ToString()),
        Data = new Input<string?>(context.Data)
    };
}
