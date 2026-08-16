using FluentValidation;

namespace R2WAI.Application.Features.TestCases.Commands;

public record UpdateTestCaseCommand : IRequest<TestCaseDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string? ExpectedResponseContains { get; init; }
    public string? ExpectedCapabilityCalled { get; init; }
}

public class UpdateTestCaseCommandValidator : AbstractValidator<UpdateTestCaseCommand>
{
    public UpdateTestCaseCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(500);
        RuleFor(v => v.Question).NotEmpty().MaximumLength(4000);
        RuleFor(v => v.ExpectedResponseContains).MaximumLength(1000);
        RuleFor(v => v.ExpectedCapabilityCalled).MaximumLength(500);
    }
}

public class UpdateTestCaseCommandHandler(
    IRepository<TestCase> testCaseRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateTestCaseCommand, TestCaseDto>
{
    public async Task<TestCaseDto> Handle(UpdateTestCaseCommand command, CancellationToken cancellationToken)
    {
        var testCase = await testCaseRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TestCase), command.Id);

        testCase.Update(command.Name, command.Question, command.ExpectedResponseContains, command.ExpectedCapabilityCalled);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<TestCaseDto>(testCase);
    }
}
