namespace R2WAI.Application.Features.Applications.DTOs;

public class ApplicationDiscoveryResultDto
{
    public Guid ApplicationApiId { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public int OperationsDiscovered { get; init; }
    public int CapabilitiesCreated { get; init; }
    public int CapabilitiesSkippedAsExisting { get; init; }
}
