namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Reads the tenant's active "ToolExecution" GlobalPolicy and exposes its optional structured risk
/// ceiling (if the admin configured one) to the Tool/API Gateway's governance check.
/// </summary>
public interface IToolExecutionPolicyService
{
    /// <summary>Null when no active policy is configured, or its content doesn't specify one.</summary>
    Task<string?> GetMaxRiskLevelAsync(Guid tenantId, CancellationToken ct = default);
}
