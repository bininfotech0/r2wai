using FluentValidation;
using R2WAI.Application.Common.Security;
using R2WAI.Application.Features.Tenants.DTOs;

namespace R2WAI.Application.Features.Tenants.Commands;

public record CreateTenantCommand : IRequest<TenantDto>, IAuthorizedRequest
{
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Domain { get; init; }
    public string[] RequiredRoles => ["SystemAdmin"];
}

public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        // Same shape as ConnectedApplication/Chatbot slugs elsewhere in this codebase — lowercase
        // kebab-case, safe to embed in a URL or subdomain later without re-validating.
        RuleFor(v => v.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase letters, numbers, and hyphens only.");
        RuleFor(v => v.Domain).MaximumLength(200);
    }
}

public class CreateTenantCommandHandler(
    IRepository<Tenant> tenantRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<CreateTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        // Matches CreateRoleCommandHandler's own convention for a uniqueness pre-check — the DB's
        // own unique index on Slug is still the real guarantee (ExceptionHandlingMiddleware maps a
        // raw unique-constraint violation to 409 too), this just gives a clearer field-level message
        // for the common case.
        var existing = await tenantRepo.FirstOrDefaultAsync(t => t.Slug == command.Slug, cancellationToken);
        if (existing is not null)
            throw new ValidationException("Slug", "A tenant with this slug already exists.");

        var tenant = new Tenant(Guid.NewGuid(), command.Name, command.Slug, command.Domain);

        await tenantRepo.AddAsync(tenant, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<TenantDto>(tenant);
    }
}
