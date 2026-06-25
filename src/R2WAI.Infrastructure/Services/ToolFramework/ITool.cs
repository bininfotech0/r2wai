namespace R2WAI.Infrastructure.Services.ToolFramework;

public sealed class ToolContext
{
    public Guid TenantId { get; init; }
    public Guid? UserId { get; init; }
    public Dictionary<string, object?> Parameters { get; init; } = [];
    public CancellationToken CancellationToken { get; init; }
}

public sealed class ToolResult
{
    public bool Success { get; init; }
    public string? Data { get; init; }
    public string? Error { get; init; }
    public TimeSpan? Duration { get; init; }
}

public interface ITool
{
    string Name { get; }
    string Description { get; }
    Task<ToolResult> ExecuteAsync(ToolContext context);
}
