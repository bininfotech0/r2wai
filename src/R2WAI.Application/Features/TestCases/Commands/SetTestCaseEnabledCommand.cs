namespace R2WAI.Application.Features.TestCases.Commands;

public record SetTestCaseEnabledCommand : IRequest<TestCaseDto>
{
    public Guid Id { get; init; }
    public bool IsEnabled { get; init; }
}

public class SetTestCaseEnabledCommandHandler(
    IRepository<TestCase> testCaseRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<SetTestCaseEnabledCommand, TestCaseDto>
{
    public async Task<TestCaseDto> Handle(SetTestCaseEnabledCommand command, CancellationToken cancellationToken)
    {
        var testCase = await testCaseRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TestCase), command.Id);

        testCase.SetEnabled(command.IsEnabled);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<TestCaseDto>(testCase);
    }
}
