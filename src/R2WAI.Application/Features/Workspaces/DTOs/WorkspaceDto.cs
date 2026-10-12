namespace R2WAI.Application.Features.Workspaces.DTOs;

public record WorkspaceResourceCounts(int Agents, int Knowledge, int Automations, int Chatbots, int Connections)
{
    public static readonly WorkspaceResourceCounts Empty = new(0, 0, 0, 0, 0);
    public int Total => Agents + Knowledge + Automations + Chatbots + Connections;
}

public class WorkspaceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
    public WorkspaceResourceCounts Resources { get; init; } = WorkspaceResourceCounts.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }

    public static WorkspaceDto From(Workspace workspace, WorkspaceResourceCounts? resources = null) => new()
    {
        Id = workspace.Id,
        Name = workspace.Name,
        Description = workspace.Description,
        IsDefault = workspace.IsDefault,
        Resources = resources ?? WorkspaceResourceCounts.Empty,
        CreatedAt = workspace.CreatedAt,
        ModifiedAt = workspace.ModifiedAt,
    };
}
