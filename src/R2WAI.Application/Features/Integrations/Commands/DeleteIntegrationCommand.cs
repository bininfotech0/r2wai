namespace R2WAI.Application.Features.Integrations.Commands;

public record DeleteIntegrationCommand : IRequest
{
    public Guid Id { get; init; }
}
