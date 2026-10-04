using FluentValidation;
using R2WAI.Application.Common.Security;
using R2WAI.Domain.Enums;

namespace R2WAI.Application.Features.Tenants.Commands;

// "Delete" here means Suspended + soft-deleted, not permanent data loss — same SoftDelete()
// convention as every other entity in this app (see DeleteAssistantCommandHandler). Also flips
// Status to Suspended so AuthController's new tenant-status enforcement actually blocks the
// tenant's users, not just hides the row from this admin listing — soft-deleting alone would have
// left every one of that tenant's users still able to log in, since Login never checked IsDeleted
// on the Tenant, only on the User.
public record DeleteTenantCommand : IRequest<Unit>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string[] RequiredRoles => ["SystemAdmin"];
}

public class DeleteTenantCommandValidator : AbstractValidator<DeleteTenantCommand>
{
    public DeleteTenantCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class DeleteTenantCommandHandler(
    IRepository<Tenant> tenantRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<DeleteTenantCommand, Unit>
{
    public async Task<Unit> Handle(DeleteTenantCommand command, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), command.Id);

        // Same self-lockout guard as UpdateTenantCommand's Status change — deleting is a stronger
        // version of the exact same mistake.
        if (tenant.Id == currentUser.TenantId)
            throw new Application.Common.Exceptions.ValidationException("Id", "You cannot delete your own current organisation.");

        tenant.UpdateStatus(TenantStatus.Suspended);
        tenant.SoftDelete();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
