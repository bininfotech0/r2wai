namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Reads the tenant's active "AiUsage" GlobalPolicy and enforces its optional daily request cap
/// against a rolling per-tenant-per-day counter. Additive-tightening only: a tenant with no policy
/// configured, or one whose content isn't the structured {"maxRequestsPerDay":N} shape, is always
/// allowed — byte-identical to pre-Policy-Engine behavior.
/// </summary>
public interface IAiUsagePolicyService
{
    /// <summary>
    /// True if this tenant is still under its configured cap (or has no cap configured). Does not
    /// itself record the request — call <see cref="RecordRequestAsync"/> once the caller has decided
    /// to actually proceed, so a denied request doesn't count against tomorrow's window.
    /// </summary>
    Task<bool> IsUnderCapAsync(Guid tenantId, CancellationToken ct = default);

    Task RecordRequestAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Read-only snapshot for display (e.g. a usage card) — never used for the allow/deny
    /// decision itself, that stays IsUnderCapAsync's job. DailyCap is null when no policy is active
    /// or its content doesn't parse, matching IsUnderCapAsync's own "always allowed" case.</summary>
    Task<AiUsageStatus> GetStatusAsync(Guid tenantId, CancellationToken ct = default);
}

public record AiUsageStatus(int? DailyCap, int CurrentDailyCount);
