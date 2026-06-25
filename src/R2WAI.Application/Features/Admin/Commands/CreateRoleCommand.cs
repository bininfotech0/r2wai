using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record CreateRoleCommand : IRequest<RoleDto>
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Permissions { get; init; }
}

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(100);
    }
}

public class CreateRoleCommandHandler(
    IRepository<Role> roleRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var existing = await roleRepo.FirstOrDefaultAsync(
            r => r.Name == command.Name && r.TenantId == tenantId && !r.IsDeleted, cancellationToken);
        if (existing != null)
            throw new ValidationException("Name", "A role with this name already exists.");

        var role = new Role(Guid.NewGuid(), tenantId, command.Name, command.Description);
        if (!string.IsNullOrEmpty(command.Permissions))
            role.SetPermissions(command.Permissions);

        await roleRepo.AddAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<RoleDto>(role);
    }
}
