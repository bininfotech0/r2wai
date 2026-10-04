using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Security;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.Authentication;

public class AuthPolicyService : IAuthPolicyService
{
    private const string PolicyType = "Auth";
    private readonly IRepository<GlobalPolicy> _policies;
    private readonly IConfiguration _configuration;

    public AuthPolicyService(IRepository<GlobalPolicy> policies, IConfiguration configuration)
    {
        _policies = policies;
        _configuration = configuration;
    }

    public async Task<bool> IsMfaRequiredAsync(Guid tenantId, CancellationToken ct = default)
    {
        var policy = await _policies.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Type == PolicyType && p.IsActive, ct);

        return AuthPolicyEvaluator.TryParseRequireMfa(policy?.Content);
    }

    public async Task<int?> GetMaxPasswordAgeDaysAsync(Guid tenantId, CancellationToken ct = default)
    {
        var policy = await _policies.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Type == PolicyType && p.IsActive, ct);

        return AuthPolicyEvaluator.TryParseMaxPasswordAgeDays(policy?.Content);
    }

    public bool IsSsoConfigured()
    {
        return !string.IsNullOrWhiteSpace(_configuration["Authentication:EntraId:TenantId"])
            && !string.IsNullOrWhiteSpace(_configuration["Authentication:EntraId:ClientId"]);
    }
}
