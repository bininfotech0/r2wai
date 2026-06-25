namespace R2WAI.Application.Features.Integrations.Commands;

public record ToggleIntegrationCommand : IRequest<bool>
{
    public Guid Id { get; init; }
}
