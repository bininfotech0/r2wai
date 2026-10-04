namespace R2WAI.Application.Features.Governance.Queries;

public record GetAuthSecurityStatusQuery : IRequest<AuthSecurityStatusDto>;

public class GetAuthSecurityStatusQueryHandler(
    IRepository<User> users,
    ICurrentUserService currentUser,
    IAuthPolicyService authPolicyService) : IRequestHandler<GetAuthSecurityStatusQuery, AuthSecurityStatusDto>
{
    public async Task<AuthSecurityStatusDto> Handle(GetAuthSecurityStatusQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var totalUsers = await users.CountAsync(u => u.TenantId == tenantId, cancellationToken);
        var mfaEnabledUsers = await users.CountAsync(u => u.TenantId == tenantId && u.MfaEnabled, cancellationToken);
        var requireMfaActive = await authPolicyService.IsMfaRequiredAsync(tenantId, cancellationToken);

        return new AuthSecurityStatusDto
        {
            TotalUsers = totalUsers,
            MfaEnabledUsers = mfaEnabledUsers,
            SsoConfigured = authPolicyService.IsSsoConfigured(),
            RequireMfaPolicyActive = requireMfaActive,
        };
    }
}
