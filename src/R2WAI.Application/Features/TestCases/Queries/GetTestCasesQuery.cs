namespace R2WAI.Application.Features.TestCases.Queries;

public record GetTestCasesQuery : IRequest<PagedResult<TestCaseDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid? AssistantId { get; init; }
    public Guid? ApplicationId { get; init; }
}

public class GetTestCasesQueryHandler(
    IRepository<TestCase> testCaseRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetTestCasesQuery, PagedResult<TestCaseDto>>
{
    public async Task<PagedResult<TestCaseDto>> Handle(GetTestCasesQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var searchTerm = query.Search?.ToLower();

        var filtered = await testCaseRepo.FindAsync(
            t => t.TenantId == tenantId
              && (string.IsNullOrEmpty(searchTerm) || t.Name.ToLower().Contains(searchTerm))
              && (!query.AssistantId.HasValue || t.AssistantId == query.AssistantId)
              && (!query.ApplicationId.HasValue || t.ApplicationId == query.ApplicationId),
            cancellationToken);

        var ordered = filtered.OrderByDescending(t => t.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<TestCaseDto>
        {
            Items = mapper.Map<List<TestCaseDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
