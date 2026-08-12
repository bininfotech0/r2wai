namespace R2WAI.Application.Features.Navigation.Queries;

public record GetNavigationItemsQuery : IRequest<List<NavigationItemDto>>
{
    public Guid ApplicationId { get; init; }
}

public class GetNavigationItemsQueryHandler(
    IRepository<NavigationDefinition> navRepo,
    IMapper mapper) : IRequestHandler<GetNavigationItemsQuery, List<NavigationItemDto>>
{
    public async Task<List<NavigationItemDto>> Handle(GetNavigationItemsQuery query, CancellationToken cancellationToken)
    {
        var items = await navRepo.FindAsync(
            n => n.ApplicationId == query.ApplicationId && !n.IsDeleted, cancellationToken);

        var ordered = items.OrderBy(n => n.Order).ToList();
        return mapper.Map<List<NavigationItemDto>>(ordered);
    }
}
