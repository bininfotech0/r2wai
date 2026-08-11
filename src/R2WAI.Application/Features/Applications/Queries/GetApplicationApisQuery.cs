namespace R2WAI.Application.Features.Applications.Queries;

public record GetApplicationApisQuery : IRequest<List<ApplicationApiDto>>
{
    public Guid ApplicationId { get; init; }
}

public class GetApplicationApisQueryHandler(
    IRepository<ApplicationApi> apiRepo,
    IMapper mapper) : IRequestHandler<GetApplicationApisQuery, List<ApplicationApiDto>>
{
    public async Task<List<ApplicationApiDto>> Handle(GetApplicationApisQuery query, CancellationToken cancellationToken)
    {
        var apis = await apiRepo.FindAsync(a => a.ApplicationId == query.ApplicationId, cancellationToken);
        var ordered = apis.OrderBy(a => a.Name).ToList();

        return mapper.Map<List<ApplicationApiDto>>(ordered);
    }
}
