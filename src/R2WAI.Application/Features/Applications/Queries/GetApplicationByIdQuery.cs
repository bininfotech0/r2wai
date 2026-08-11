namespace R2WAI.Application.Features.Applications.Queries;

public record GetApplicationByIdQuery : IRequest<ApplicationDto>
{
    public Guid Id { get; init; }
}

public class GetApplicationByIdQueryHandler(
    IRepository<ConnectedApplication> applicationRepo,
    IMapper mapper) : IRequestHandler<GetApplicationByIdQuery, ApplicationDto>
{
    public async Task<ApplicationDto> Handle(GetApplicationByIdQuery query, CancellationToken cancellationToken)
    {
        var application = await applicationRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ConnectedApplication), query.Id);

        if (application.IsDeleted)
            throw new NotFoundException(nameof(ConnectedApplication), query.Id);

        return mapper.Map<ApplicationDto>(application);
    }
}
