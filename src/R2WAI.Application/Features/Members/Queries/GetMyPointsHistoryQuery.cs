namespace R2WAI.Application.Features.Members.Queries;

public record GetMyPointsHistoryQuery : IRequest<PagedResult<PointsTransactionDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetMyPointsHistoryQueryHandler(
    IRepository<PointsTransaction> transactionRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetMyPointsHistoryQuery, PagedResult<PointsTransactionDto>>
{
    public async Task<PagedResult<PointsTransactionDto>> Handle(GetMyPointsHistoryQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var all = await transactionRepo.FindAsync(t => t.UserId == userId, cancellationToken);
        var ordered = all.OrderByDescending(t => t.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<PointsTransactionDto>
        {
            Items = mapper.Map<List<PointsTransactionDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
