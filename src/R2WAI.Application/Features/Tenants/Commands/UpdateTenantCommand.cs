using FluentValidation;
using R2WAI.Application.Common.Security;
using R2WAI.Application.Features.Tenants.DTOs;
using R2WAI.Domain.Enums;

namespace R2WAI.Application.Features.Tenants.Commands;

public record UpdateTenantCommand : IRequest<TenantDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Domain { get; init; }
    public TenantStatus? Status { get; init; }
    public string[] RequiredRoles => ["SystemAdmin"];
}

public class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase letters, numbers, and hyphens only.");
        RuleFor(v => v.Domain).MaximumLength(200);
    }
}

public class UpdateTenantCommandHandler(
    IRepository<Tenant> tenantRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<UpdateTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(UpdateTenantCommand command, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), command.Id);

        var slugTaken = await tenantRepo.FirstOrDefaultAsync(t => t.Slug == command.Slug && t.Id != command.Id, cancellationToken);
        if (slugTaken is not null)
            throw new ValidationException("Slug", "A tenant with this slug already exists.");

        tenant.UpdateDetails(command.Name, command.Slug, command.Domain);

        if (command.Status.HasValue && command.Status.Value != tenant.Status)
        {
            // A SystemAdmin suspending their own currently-active tenant would lock themselves
            // (and everyone else in it) out on their very next token refresh — AuthController.Login/
            // Refresh/ExchangeEntraIdToken all now enforce Status == Active for real. Not a
            // theoretical guard: this endpoint is the only place that can flip it.
            if (command.Status.Value != TenantStatus.Active && tenant.Id == currentUser.TenantId)
                throw new ValidationException("Status", "You cannot suspend or deactivate your own current organisation.");

            tenant.UpdateStatus(command.Status.Value);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<TenantDto>(tenant);
    }
}
