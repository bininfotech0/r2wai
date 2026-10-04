using Elsa.Email.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Models;

namespace R2WAI.Api.Workflows.NodeProviders;

public sealed class EmailNodeProvider : INodeProvider
{
    public string StepType => "Email";

    public IActivity CreateActivity(NodeCreationContext context)
    {
        var config = context.Config;
        return new SendEmail
        {
            Name = context.Step.Name,
            To = new Input<ICollection<string>>(SplitAddresses(config?.EmailTo)),
            Cc = new Input<ICollection<string>>(SplitAddresses(config?.EmailCc)),
            Subject = new Input<string?>(config?.EmailSubject ?? context.Step.Name),
            Body = new Input<string>(config?.EmailBody ?? string.Empty)
        };
    }

    private static ICollection<string> SplitAddresses(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
