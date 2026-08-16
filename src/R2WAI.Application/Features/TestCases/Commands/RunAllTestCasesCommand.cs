namespace R2WAI.Application.Features.TestCases.Commands;

public record RunAllTestCasesCommand : IRequest<TestRunDto>
{
    public Guid? AssistantId { get; init; }
    public Guid? ApplicationId { get; init; }
}

public class RunAllTestCasesCommandHandler(
    IRepository<TestCase> testCaseRepo,
    IRepository<TestRun> testRunRepo,
    IRepository<TestCaseResult> testCaseResultRepo,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<RunAllTestCasesCommand, TestRunDto>
{
    public async Task<TestRunDto> Handle(RunAllTestCasesCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var testCases = await testCaseRepo.FindAsync(
            t => t.TenantId == tenantId && t.IsEnabled
              && (!command.AssistantId.HasValue || t.AssistantId == command.AssistantId)
              && (!command.ApplicationId.HasValue || t.ApplicationId == command.ApplicationId),
            cancellationToken);

        var testRun = new TestRun(Guid.NewGuid(), tenantId, userId, command.AssistantId, command.ApplicationId);
        await testRunRepo.AddAsync(testRun, cancellationToken);

        var results = new List<TestCaseResult>();
        foreach (var testCase in testCases)
        {
            var result = await TestCaseExecutor.ExecuteAsync(mediator, testRun.Id, testCase, cancellationToken);
            await testCaseResultRepo.AddAsync(result, cancellationToken);
            results.Add(result);
        }

        testRun.Complete(
            passedCount: results.Count(r => r.Status == TestCaseResultStatus.Passed),
            failedCount: results.Count(r => r.Status == TestCaseResultStatus.Failed),
            warningCount: results.Count(r => r.Status == TestCaseResultStatus.Warning));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = mapper.Map<TestRunDto>(testRun);
        dto.Results.AddRange(mapper.Map<List<TestCaseResultDto>>(results));
        return dto;
    }
}
