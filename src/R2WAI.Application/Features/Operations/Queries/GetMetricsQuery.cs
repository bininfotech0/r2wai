namespace R2WAI.Application.Features.Operations.Queries;

public record GetMetricsQuery : IRequest<MetricsDto>, IAuthorizedRequest
{
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}
