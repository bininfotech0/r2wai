namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Reads the tenant's active "Auth" GlobalPolicy and exposes its optional MFA requirement to the
/// login flow (AuthController) and the Security & Policies status view.
/// </summary>
public interface IAuthPolicyService
{
    /// <summary>False when no active policy is configured, or its content doesn't require MFA.</summary>
    Task<bool> IsMfaRequiredAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Null when no active policy is configured, or its content doesn't set an expiry.</summary>
    Task<int?> GetMaxPasswordAgeDaysAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>True when Authentication:EntraId is configured (TenantId + ClientId set).</summary>
    bool IsSsoConfigured();
}
