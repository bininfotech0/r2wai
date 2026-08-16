namespace R2WAI.Application.Features.TestCases.Queries;

public record GetTestCaseByIdQuery : IRequest<TestCaseDto>
{
    public Guid Id { get; init; }
}

public class GetTestCaseByIdQueryHandler(
    IRepository<TestCase> testCaseRepo,
    IMapper mapper) : IRequestHandler<GetTestCaseByIdQuery, TestCaseDto>
{
    public async Task<TestCaseDto> Handle(GetTestCaseByIdQuery query, CancellationToken cancellationToken)
    {
        var testCase = await testCaseRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TestCase), query.Id);

        return mapper.Map<TestCaseDto>(testCase);
    }
}
