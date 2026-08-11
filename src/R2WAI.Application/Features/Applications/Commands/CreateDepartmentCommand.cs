using FluentValidation;

namespace R2WAI.Application.Features.Applications.Commands;

public record CreateDepartmentCommand : IRequest<DepartmentDto>
{
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? HeadUserId { get; init; }
}

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Code).NotEmpty().MaximumLength(50);
        RuleFor(v => v.Description).MaximumLength(2000);
    }
}

public class CreateDepartmentCommandHandler(
    IRepository<Department> departmentRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var department = new Department(
            Guid.NewGuid(), tenantId, command.Name, command.Code, command.Description);

        department.SetHead(command.HeadUserId);

        await departmentRepo.AddAsync(department, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<DepartmentDto>(department);
    }
}
