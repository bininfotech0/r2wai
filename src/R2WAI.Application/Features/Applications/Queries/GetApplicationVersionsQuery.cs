namespace R2WAI.Application.Features.Applications.Queries;

public record GetApplicationVersionsQuery : IRequest<List<ApplicationVersionDto>>
{
    public Guid ApplicationId { get; init; }
}

public class GetApplicationVersionsQueryHandler(
    IRepository<ApplicationVersion> versionRepo,
    IMapper mapper) : IRequestHandler<GetApplicationVersionsQuery, List<ApplicationVersionDto>>
{
    public async Task<List<ApplicationVersionDto>> Handle(GetApplicationVersionsQuery query, CancellationToken cancellationToken)
    {
        var versions = await versionRepo.FindAsync(v => v.ApplicationId == query.ApplicationId, cancellationToken);
        var ordered = versions.OrderByDescending(v => v.VersionNumber).ToList();

        return mapper.Map<List<ApplicationVersionDto>>(ordered);
    }
}
