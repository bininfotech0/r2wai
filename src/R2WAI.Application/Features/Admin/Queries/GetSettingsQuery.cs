namespace R2WAI.Application.Features.Admin.Queries;

public record GetSettingsQuery : IRequest<SettingsDto> { }

public class GetSettingsQueryHandler(
    IRepository<Tenant> tenantRepo,
    ICurrentUserService currentUser) : IRequestHandler<GetSettingsQuery, SettingsDto>
{
    public async Task<SettingsDto> Handle(GetSettingsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var tenant = await tenantRepo.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        return new SettingsDto
        {
            TenantId = tenant.Id,
            TenantSettings = tenant.Settings,
            Features = tenant.Features,
        };
    }
}
