namespace R2WAI.Application.Features.Proposals.Queries;

public record GetProposalsQuery : IRequest<PagedResult<ProposalDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetProposalsQueryHandler(
    ICurrentUserService currentUser) : IRequestHandler<GetProposalsQuery, PagedResult<ProposalDto>>
{
    public Task<PagedResult<ProposalDto>> Handle(GetProposalsQuery query, CancellationToken cancellationToken)
    {
        _ = currentUser.TenantId ?? throw new UnauthorizedException();

        return Task.FromResult(new PagedResult<ProposalDto>
        {
            Items = [],
            TotalCount = 0,
            Page = query.Page,
            PageSize = query.PageSize,
        });
    }
}
