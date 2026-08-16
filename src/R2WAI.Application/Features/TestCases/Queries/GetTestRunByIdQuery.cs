namespace R2WAI.Application.Features.TestCases.Queries;

public record GetTestRunByIdQuery : IRequest<TestRunDto>
{
    public Guid Id { get; init; }
}

public class GetTestRunByIdQueryHandler(
    IRepository<TestRun> testRunRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetTestRunByIdQuery, TestRunDto>
{
    public async Task<TestRunDto> Handle(GetTestRunByIdQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var testRun = await testRunRepo.GetByIdAsync(query.Id, r => r.Results, cancellationToken)
            ?? throw new NotFoundException(nameof(TestRun), query.Id);

        if (testRun.TenantId != tenantId)
            throw new NotFoundException(nameof(TestRun), query.Id);

        return mapper.Map<TestRunDto>(testRun);
    }
}
