using FluentValidation;
using R2WAI.Application.Common.Validation;

namespace R2WAI.Application.Features.Applications.Commands;

public record CreateApplicationCommand : IRequest<ApplicationDto>
{
    public Guid? DepartmentId { get; init; }
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
        // DepartmentId is intentionally unvalidated: a connected system must be creatable without
        // an organisational container (R2WAI 2.0 product decision). The handler still rejects a
        // supplied id that does not resolve inside the caller's tenant.
        RuleFor(v => v.Name).NotEmpty().MaximumLength(500);
        RuleFor(v => v.Code).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Description).MaximumLength(2000);
        RuleFor(v => v.BaseUrl).MaximumLength(500).MustBeValidHttpUrl();
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

        if (command.DepartmentId.HasValue)
        {
            var department = await departmentRepo.GetByIdAsync(command.DepartmentId.Value, cancellationToken);

            // Null and cross-tenant are deliberately indistinguishable: a caller must not be able
            // to probe another tenant's department ids by comparing error responses.
            if (department is null || department.TenantId != tenantId)
                throw new NotFoundException(nameof(Department), command.DepartmentId.Value);
        }

        // Code uniqueness is enforced by the database, but the raw Postgres error is poor UX, and
        // the constraint differs by scope: department-scoped systems are unique per
        // (tenant, department, code), department-less ones per (tenant, code) via a filtered index.
        // The tenant filter is applied by the global query filter, so this cannot see other tenants.
        // This check is a courtesy for the user; the unique index remains the authority.
        var existingWithCode = await applicationRepo.FirstOrDefaultAsync(
            a => a.Code == command.Code && a.DepartmentId == command.DepartmentId,
            cancellationToken);

        if (existingWithCode is not null)
            throw new ValidationException(nameof(command.Code),
                $"Code '{command.Code}' is already used by another connected system in this scope.");

        var application = new ConnectedApplication(
            Guid.NewGuid(), tenantId, command.DepartmentId, command.Name, command.Code,
            command.Description, command.BaseUrl);

        application.UpdateDetails(command.Name, command.Description, command.BaseUrl, command.Environment);

        await applicationRepo.AddAsync(application, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ApplicationDto>(application);
    }
}
