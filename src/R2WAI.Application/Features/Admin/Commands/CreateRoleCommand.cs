using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record CreateRoleCommand : IRequest<RoleDto>, IAuthorizedRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Permissions { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
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
    ICacheService cacheService,
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

        await InvalidateRoleCacheAsync(tenantId, cancellationToken);

        return mapper.Map<RoleDto>(role);
    }

    private async Task InvalidateRoleCacheAsync(Guid tenantId, CancellationToken ct)
    {
        for (var page = 1; page <= 10; page++)
        {
            foreach (var size in new[] { 20, 50, 100 })
                await cacheService.RemoveAsync($"roles:{tenantId}:p{page}:s{size}", ct);
        }
    }
}
