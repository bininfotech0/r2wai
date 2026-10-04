using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record UpdateSettingsCommand : IRequest<SettingsDto>, IAuthorizedRequest
{
    // Organization Details (General settings) — Tenant.Name/Slug/Domain are
    // real fields with a real Tenant.UpdateDetails method; they just weren't
    // exposed through this DTO/command before, so this page silently no-opped.
    public string? TenantName { get; init; }
    public string? TenantSlug { get; init; }
    public string? TenantDomain { get; init; }
    public string? TenantSettings { get; init; }
    public string? Features { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class UpdateSettingsCommandValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsCommandValidator()
    {
        // Both fields are opaque JSON blobs merged into Tenant.Settings/Features (e.g. the
        // content-moderation panel stores its rules as a "contentModeration" key inside
        // TenantSettings). Nothing validated this before persisting — a malformed write here
        // wouldn't crash on save, but would silently replace whatever valid JSON was there,
        // corrupting every other settings section for the tenant. Same class of gap as D6's
        // unvalidated cron expressions, just for tenant-wide config instead of a schedule.
        RuleFor(v => v.TenantSettings)
            .Must(BeValidJsonOrNull).WithMessage("Tenant settings must be valid JSON.")
            .When(v => v.TenantSettings is not null);

        RuleFor(v => v.Features)
            .Must(BeValidJsonOrNull).WithMessage("Features must be valid JSON.")
            .When(v => v.Features is not null);
    }

    private static bool BeValidJsonOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try { System.Text.Json.JsonDocument.Parse(json); return true; }
        catch (System.Text.Json.JsonException) { return false; }
    }
}

public class UpdateSettingsCommandHandler(
    IRepository<Tenant> tenantRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<UpdateSettingsCommand, SettingsDto>
{
    public async Task<SettingsDto> Handle(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var tenant = await tenantRepo.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        if (command.TenantName is not null)
            tenant.UpdateDetails(command.TenantName, command.TenantSlug ?? tenant.Slug, command.TenantDomain);

        if (command.TenantSettings is not null)
            tenant.UpdateSettings(command.TenantSettings);

        if (command.Features is not null)
            tenant.UpdateFeatures(command.Features);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SettingsDto
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            TenantSlug = tenant.Slug,
            TenantDomain = tenant.Domain,
            TenantSettings = tenant.Settings,
            Features = tenant.Features,
        };
    }
}
