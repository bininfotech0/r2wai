using FluentValidation;

namespace R2WAI.Application.Features.TestCases.Commands;

public record CreateTestCaseCommand : IRequest<TestCaseDto>
{
    public Guid AssistantId { get; init; }
    public Guid? ApplicationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string? ExpectedResponseContains { get; init; }
    public string? ExpectedCapabilityCalled { get; init; }
}

public class CreateTestCaseCommandValidator : AbstractValidator<CreateTestCaseCommand>
{
    public CreateTestCaseCommandValidator()
    {
        RuleFor(v => v.AssistantId).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(500);
        RuleFor(v => v.Question).NotEmpty().MaximumLength(4000);
        RuleFor(v => v.ExpectedResponseContains).MaximumLength(1000);
        RuleFor(v => v.ExpectedCapabilityCalled).MaximumLength(500);
    }
}

public class CreateTestCaseCommandHandler(
    IRepository<TestCase> testCaseRepo,
    IRepository<AssistantDefinition> assistantRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateTestCaseCommand, TestCaseDto>
{
    public async Task<TestCaseDto> Handle(CreateTestCaseCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        _ = await assistantRepo.GetByIdAsync(command.AssistantId, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.AssistantId);

        var testCase = new TestCase(Guid.NewGuid(), tenantId, command.AssistantId, command.Name, command.Question,
            command.ApplicationId, command.ExpectedResponseContains, command.ExpectedCapabilityCalled);

        await testCaseRepo.AddAsync(testCase, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<TestCaseDto>(testCase);
    }
}
