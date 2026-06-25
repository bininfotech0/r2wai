namespace R2WAI.Application.Features.Admin.DTOs;

public class AuditLogDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public AuditAction Action { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public string? IpAddress { get; init; }
    public DateTime Timestamp { get; init; }
}
