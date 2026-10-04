using Elsa.Workflows;
using R2WAI.Application.Features.Workflows.DTOs;

namespace R2WAI.Api.Workflows.NodeProviders;

/// <summary>
/// Builds the Elsa activity for one workflow step type. One implementation per step type, each
/// registered in DI and collected via <c>IEnumerable&lt;INodeProvider&gt;</c> — see
/// <see cref="StepActivityFactory"/>, which looks providers up by <see cref="StepType"/> instead of
/// switching on a hardcoded case list. Adding a new step type means adding a new class + DI
/// registration, no edit to StepActivityFactory itself (Track B Phase 5's "compiled-but-decoupled"
/// scope — still needs a recompile, not a true assembly-loaded plugin).
/// </summary>
public interface INodeProvider
{
    /// <summary>The key <see cref="StepActivityFactory.ClassifyStepType"/> produces for this step type.</summary>
    string StepType { get; }

    IActivity CreateActivity(NodeCreationContext context);
}

/// <summary>Everything a node provider needs to build its activity — mirrors CreateActivityForStep's old parameter list plus the already-parsed step config.</summary>
public sealed record NodeCreationContext(
    WorkflowStepDto Step, Guid WorkflowId, Guid TenantId, Guid UserId, Guid InstanceId, string? Data, StepConfigDto? Config);

/// <summary>
/// The step-specific configuration fields a step's Config JSON can carry. Shared across providers
/// (each reads only the fields relevant to its own step type) rather than duplicated per provider.
/// </summary>
public sealed class StepConfigDto
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
