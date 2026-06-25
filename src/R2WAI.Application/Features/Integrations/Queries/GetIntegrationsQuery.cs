namespace R2WAI.Application.Features.Integrations.Queries;

public record GetIntegrationsQuery : IRequest<PagedResult<IntegrationDto>>
{
    public string? Search { get; init; }
    public string? Category { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
