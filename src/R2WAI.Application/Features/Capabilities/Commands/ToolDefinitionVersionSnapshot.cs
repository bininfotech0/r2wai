using System.Text.Json;

namespace R2WAI.Application.Features.Capabilities.Commands;

internal class ToolDefinitionConfigSnapshot
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ToolType { get; init; } = string.Empty;
    public string? EndpointUrl { get; init; }
    public string? HttpMethod { get; init; }
    public string? EndpointPath { get; init; }
    public string? Configuration { get; init; }
    public Guid? ApplicationApiId { get; init; }
    public string RiskLevel { get; init; } = "Low";
    public string? RequiredRole { get; init; }
    public bool ConfirmationRequired { get; init; }
    public bool ApprovalRequired { get; init; }
    public bool AuditRequired { get; init; } = true;
}

/// <summary>
/// Builds the JSON snapshot stored on a <see cref="ToolDefinitionVersion"/> and restores it back onto
/// a live <see cref="ToolDefinition"/> during rollback. Shared by <see cref="CreateToolDefinitionVersionCommandHandler"/>
/// and <see cref="RollbackToolDefinitionVersionCommandHandler"/> so both always snapshot the same shape
/// — mirrors ApplicationVersionSnapshotService's role for ApplicationVersion.
/// </summary>
internal static class ToolDefinitionVersionSnapshotService
{
    public static string BuildSnapshotJson(ToolDefinition tool) =>
        JsonSerializer.Serialize(new ToolDefinitionConfigSnapshot
        {
            Name = tool.Name,
            Description = tool.Description,
            ToolType = tool.ToolType.ToString(),
            EndpointUrl = tool.EndpointUrl,
            HttpMethod = tool.HttpMethod,
            EndpointPath = tool.EndpointPath,
            Configuration = tool.Configuration,
            ApplicationApiId = tool.ApplicationApiId,
            RiskLevel = tool.RiskLevel,
            RequiredRole = tool.RequiredRole,
            ConfirmationRequired = tool.ConfirmationRequired,
            ApprovalRequired = tool.ApprovalRequired,
            AuditRequired = tool.AuditRequired
        });

    public static ToolDefinitionConfigSnapshot Deserialize(string configSnapshotJson) =>
        JsonSerializer.Deserialize<ToolDefinitionConfigSnapshot>(configSnapshotJson)
            ?? throw new InvalidOperationException("Stored tool definition version snapshot could not be read.");
}
