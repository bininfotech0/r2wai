using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class AuditLog : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public AuditAction Action { get; private set; }
    public string EntityType { get; private set; }
    public string EntityId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string? Metadata { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public User? User { get; private set; }

    private AuditLog() { }

    public AuditLog(Guid id, Guid tenantId, AuditAction action, string entityType,
                    string entityId, Guid? userId = null, string? oldValues = null,
                    string? newValues = null, string? ipAddress = null,
                    string? userAgent = null, string? metadata = null)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OldValues = oldValues;
        NewValues = newValues;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Timestamp = DateTime.UtcNow;
        Metadata = metadata;
        CreatedAt = DateTime.UtcNow;
    }
}
