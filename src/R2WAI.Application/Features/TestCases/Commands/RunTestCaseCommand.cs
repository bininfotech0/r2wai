namespace R2WAI.Application.Features.TestCases.Commands;

public record RunTestCaseCommand : IRequest<TestRunDto>
{
    public Guid TestCaseId { get; init; }
}

public class RunTestCaseCommandHandler(
    IRepository<TestCase> testCaseRepo,
    IRepository<TestRun> testRunRepo,
    IRepository<TestCaseResult> testCaseResultRepo,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<RunTestCaseCommand, TestRunDto>
{
    public async Task<TestRunDto> Handle(RunTestCaseCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var testCase = await testCaseRepo.GetByIdAsync(command.TestCaseId, cancellationToken)
            ?? throw new NotFoundException(nameof(TestCase), command.TestCaseId);

        var testRun = new TestRun(Guid.NewGuid(), tenantId, userId, testCase.AssistantId, testCase.ApplicationId);
        await testRunRepo.AddAsync(testRun, cancellationToken);

        var result = await TestCaseExecutor.ExecuteAsync(mediator, testRun.Id, testCase, cancellationToken);
        await testCaseResultRepo.AddAsync(result, cancellationToken);

        testRun.Complete(
            passedCount: result.Status == TestCaseResultStatus.Passed ? 1 : 0,
            failedCount: result.Status == TestCaseResultStatus.Failed ? 1 : 0,
            warningCount: result.Status == TestCaseResultStatus.Warning ? 1 : 0);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = mapper.Map<TestRunDto>(testRun);
        dto.Results.Add(mapper.Map<TestCaseResultDto>(result));
        return dto;
    }
}
