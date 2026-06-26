using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record UpdateRoleCommand : IRequest<RoleDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Permissions { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(100);
    }
}

public class UpdateRoleCommandHandler(
    IRepository<Role> roleRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICacheService cacheService,
    IMapper mapper) : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await roleRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), command.Id);

        role.UpdateDetails(command.Name, command.Description);
        if (command.Permissions is not null)
            role.SetPermissions(command.Permissions);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            for (var page = 1; page <= 10; page++)
            {
                foreach (var size in new[] { 20, 50, 100 })
                    await cacheService.RemoveAsync($"roles:{tenantId}:p{page}:s{size}", cancellationToken);
            }
        }

        return mapper.Map<RoleDto>(role);
    }
}
