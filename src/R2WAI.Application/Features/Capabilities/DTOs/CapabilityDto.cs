namespace R2WAI.Application.Features.Capabilities.DTOs;

public class CapabilityDto
{
    public Guid Id { get; init; }
    public Guid? ApplicationId { get; init; }
    public Guid? ApplicationApiId { get; init; }
    // Resolved via AutoMapper's flattening convention from ToolDefinition.ApplicationApi.Name —
    // requires the query to eager-load that navigation property, since it's never lazy-loaded here.
    // Null for a standalone/direct-endpoint tool with no ApplicationApiId.
    public string? ApplicationApiName { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? HttpMethod { get; init; }
    public string? EndpointPath { get; init; }
    public bool IsActive { get; init; }
    // Real, persisted result of the last "Run Test" call (Integrations and Capabilities share the
    // same ToolDefinition.RecordTestResult()) — null means never tested, distinct from Error.
    public string? LastTestStatus { get; init; }
    public DateTime? LastTestedAt { get; init; }
    public string RiskLevel { get; init; } = "Low";
    public string? RequiredRole { get; init; }
    public bool ConfirmationRequired { get; init; }
    public bool ApprovalRequired { get; init; }
    public bool AuditRequired { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}
