namespace R2WAI.Application.Features.Admin.Commands;

public record UpdateSettingsCommand : IRequest<SettingsDto>
{
    public string? TenantSettings { get; init; }
    public string? Features { get; init; }
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

        if (command.TenantSettings is not null)
            tenant.UpdateSettings(command.TenantSettings);

        if (command.Features is not null)
            tenant.UpdateFeatures(command.Features);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SettingsDto
        {
            TenantId = tenant.Id,
            TenantSettings = tenant.Settings,
            Features = tenant.Features,
        };
    }
}
