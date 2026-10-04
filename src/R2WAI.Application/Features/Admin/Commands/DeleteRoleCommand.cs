using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record DeleteRoleCommand : IRequest<Unit>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class DeleteRoleCommandHandler(
    IRepository<Role> roleRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICacheService cacheService) : IRequestHandler<DeleteRoleCommand, Unit>
{
    public async Task<Unit> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await roleRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), command.Id);

        if (role.IsSystem)
            throw new ValidationException("Role", "System roles cannot be deleted.");

        role.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            // Concurrent, not sequential -- 30 independent keys awaited one at a time each pays a
            // full cache-timeout penalty when the backend is slow/unreachable (measured live as a
            // multi-minute hang elsewhere; see AssistantCacheKeys.InvalidateAsync).
            var removals = Enumerable.Range(1, 10).SelectMany(page => new[] { 20, 50, 100 }
                .Select(size => cacheService.RemoveAsync($"roles:{tenantId}:p{page}:s{size}", cancellationToken)));
            await Task.WhenAll(removals);
        }

        return Unit.Value;
    }
}
