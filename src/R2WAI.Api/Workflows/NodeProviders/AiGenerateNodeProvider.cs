using Elsa.Workflows;
using Elsa.Workflows.Models;
using R2WAI.Api.Workflows;

namespace R2WAI.Api.Workflows.NodeProviders;

public sealed class AiGenerateNodeProvider : INodeProvider
{
    public string StepType => "AI Generate";

    public IActivity CreateActivity(NodeCreationContext context)
    {
        var step = context.Step;
        var config = context.Config;
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
            Temperature = new Input<double>(config?.AiTemperature ?? 0.7),
            TenantId = new Input<string>(context.TenantId.ToString())
        };
    }
}
