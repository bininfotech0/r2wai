namespace R2WAI.Application.Features.Integrations.Queries;

public record GetIntegrationByIdQuery : IRequest<IntegrationDto>
{
    public Guid Id { get; init; }
}
