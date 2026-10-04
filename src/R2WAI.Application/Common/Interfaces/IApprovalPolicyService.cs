namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Reads the tenant's active "Approval" GlobalPolicy and exposes its optional structured risk floor
/// (if the admin configured one) to the Tool/API Gateway's governance check.
/// </summary>
public interface IApprovalPolicyService
{
    /// <summary>Null when no active policy is configured, or its content doesn't specify one.</summary>
    Task<string?> GetRequireApprovalAboveRiskLevelAsync(Guid tenantId, CancellationToken ct = default);
}
