namespace R2WAI.Application.Features.Capabilities.DTOs;

public class CapabilityDto
{
    public Guid Id { get; init; }
    public Guid? ApplicationId { get; init; }
    public Guid? ApplicationApiId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? HttpMethod { get; init; }
    public string? EndpointPath { get; init; }
    public bool IsActive { get; init; }
    public string RiskLevel { get; init; } = "Low";
    public string? RequiredRole { get; init; }
    public bool ConfirmationRequired { get; init; }
    public bool ApprovalRequired { get; init; }
    public bool AuditRequired { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}
