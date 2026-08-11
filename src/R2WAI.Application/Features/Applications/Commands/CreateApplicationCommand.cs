using FluentValidation;

namespace R2WAI.Application.Features.Applications.Commands;

public record CreateApplicationCommand : IRequest<ApplicationDto>
{
    public Guid DepartmentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? BaseUrl { get; init; }
    public ApplicationEnvironment Environment { get; init; } = ApplicationEnvironment.Development;
}

public class CreateApplicationCommandValidator : AbstractValidator<CreateApplicationCommand>
{
    public CreateApplicationCommandValidator()
    {
        RuleFor(v => v.DepartmentId).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(500);
        RuleFor(v => v.Code).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Description).MaximumLength(2000);
        RuleFor(v => v.BaseUrl).MaximumLength(500);
    }
}

public class CreateApplicationCommandHandler(
    IRepository<ConnectedApplication> applicationRepo,
    IRepository<Department> departmentRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateApplicationCommand, ApplicationDto>
{
    public async Task<ApplicationDto> Handle(CreateApplicationCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var department = await departmentRepo.GetByIdAsync(command.DepartmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), command.DepartmentId);

        if (department.TenantId != tenantId)
            throw new NotFoundException(nameof(Department), command.DepartmentId);

        var application = new ConnectedApplication(
            Guid.NewGuid(), tenantId, command.DepartmentId, command.Name, command.Code,
            command.Description, command.BaseUrl);

        application.UpdateDetails(command.Name, command.Description, command.BaseUrl, command.Environment);

        await applicationRepo.AddAsync(application, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ApplicationDto>(application);
    }
}
