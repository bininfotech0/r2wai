using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record AssignUserRolesCommand : IRequest<UserDto>, IAuthorizedRequest
{
    public Guid UserId { get; init; }
    public List<Guid> RoleIds { get; init; } = [];
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class AssignUserRolesCommandValidator : AbstractValidator<AssignUserRolesCommand>
{
    public AssignUserRolesCommandValidator()
    {
        RuleFor(v => v.UserId).NotEmpty();
    }
}

public class AssignUserRolesCommandHandler(
    IRepository<User> userRepo,
    IRepository<Role> roleRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<AssignUserRolesCommand, UserDto>
{
    public async Task<UserDto> Handle(AssignUserRolesCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var user = await userRepo.GetByIdAsync(command.UserId, u => u.UserRoles, cancellationToken)
            ?? throw new NotFoundException(nameof(User), command.UserId);
        if (user.TenantId != tenantId)
            throw new NotFoundException(nameof(User), command.UserId);

        var validRoleIds = new List<Guid>();
        foreach (var roleId in command.RoleIds.Distinct())
        {
            var role = await roleRepo.GetByIdAsync(roleId, cancellationToken);
            if (role is not null && role.TenantId == tenantId)
                validRoleIds.Add(roleId);
        }

        user.AssignRoles(validRoleIds);
        await userRepo.SaveChangesAsync(cancellationToken);

        var reloaded = await userRepo.GetByIdAsync(command.UserId, "UserRoles.Role", cancellationToken);
        return mapper.Map<UserDto>(reloaded);
    }
}
