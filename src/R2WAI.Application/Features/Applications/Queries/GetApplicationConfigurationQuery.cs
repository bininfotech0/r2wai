namespace R2WAI.Application.Features.Applications.Queries;

public record GetApplicationConfigurationQuery : IRequest<ApplicationConfigurationDto>
{
    public Guid ApplicationId { get; init; }
}

public class GetApplicationConfigurationQueryHandler(
    IRepository<ApplicationConfiguration> configRepo,
    IMapper mapper) : IRequestHandler<GetApplicationConfigurationQuery, ApplicationConfigurationDto>
{
    public async Task<ApplicationConfigurationDto> Handle(GetApplicationConfigurationQuery query, CancellationToken cancellationToken)
    {
        var configuration = await configRepo.FirstOrDefaultAsync(c => c.ApplicationId == query.ApplicationId, cancellationToken);

        return configuration is null
            ? new ApplicationConfigurationDto { ApplicationId = query.ApplicationId }
            : mapper.Map<ApplicationConfigurationDto>(configuration);
    }
}
