namespace R2WAI.Application.Features.TestCases.Queries;

public record GetTestRunsQuery : IRequest<PagedResult<TestRunDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? AssistantId { get; init; }
    public Guid? ApplicationId { get; init; }
}

public class GetTestRunsQueryHandler(
    IRepository<TestRun> testRunRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetTestRunsQuery, PagedResult<TestRunDto>>
{
    public async Task<PagedResult<TestRunDto>> Handle(GetTestRunsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var filtered = await testRunRepo.FindAsync(
            t => t.TenantId == tenantId
              && (!query.AssistantId.HasValue || t.AssistantId == query.AssistantId)
              && (!query.ApplicationId.HasValue || t.ApplicationId == query.ApplicationId),
            cancellationToken);

        var ordered = filtered.OrderByDescending(t => t.StartedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<TestRunDto>
        {
            Items = mapper.Map<List<TestRunDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
