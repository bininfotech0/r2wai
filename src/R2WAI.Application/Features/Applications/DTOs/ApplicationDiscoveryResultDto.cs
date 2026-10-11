namespace R2WAI.Application.Features.Applications.DTOs;

public class ApplicationDiscoveryResultDto
{
    public Guid ApplicationApiId { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public int OperationsDiscovered { get; init; }
    public int CapabilitiesCreated { get; init; }
    /// <summary>Operations whose capability already exists at the same route.</summary>
    public int CapabilitiesSkippedAsExisting { get; init; }
    /// <summary>Inactive drafts whose operation moved to a new route and were re-pointed.</summary>
    public int CapabilitiesRelinked { get; init; }
    /// <summary>Active capabilities whose operation moved — left unchanged for an admin to review.</summary>
    public int CapabilitiesNeedingReview { get; init; }
    /// <summary>New capabilities prefixed with the system's code to avoid a tenant-wide name clash.</summary>
    public int CapabilitiesRenamedForUniqueness { get; init; }
    /// <summary>Capabilities whose route no longer appears in the spec. Never deleted automatically.</summary>
    public int StaleCapabilityCount { get; init; }
    public IReadOnlyList<string> StaleCapabilityNames { get; init; } = [];
}
