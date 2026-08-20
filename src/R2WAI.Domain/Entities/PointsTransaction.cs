using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>Append-only ledger entry — never mutated after creation, same spirit as AuditLog.</summary>
public sealed class PointsTransaction : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public int Points { get; private set; }
    public PointsTransactionReason Reason { get; private set; }
    public string? Description { get; private set; }
    public Guid? RelatedEventId { get; private set; }

    public User User { get; private set; } = null!;
    public MemberEvent? RelatedEvent { get; private set; }

    private PointsTransaction() { }

    public PointsTransaction(Guid id, Guid tenantId, Guid userId, int points, PointsTransactionReason reason,
                              string? description = null, Guid? relatedEventId = null)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Points = points;
        Reason = reason;
        Description = description;
        RelatedEventId = relatedEventId;
        CreatedAt = DateTime.UtcNow;
    }
}
