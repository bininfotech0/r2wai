namespace R2WAI.Application.Features.TestCases.Commands;

public record DeleteTestCaseCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteTestCaseCommandHandler(
    IRepository<TestCase> testCaseRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteTestCaseCommand, Unit>
{
    public async Task<Unit> Handle(DeleteTestCaseCommand command, CancellationToken cancellationToken)
    {
        var testCase = await testCaseRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TestCase), command.Id);

        testCaseRepo.Delete(testCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
