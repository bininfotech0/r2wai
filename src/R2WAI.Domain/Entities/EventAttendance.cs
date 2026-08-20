using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>Records that a member was credited points for an event. PointsAwarded is a snapshot
/// of the event's value at award time, so later edits to MemberEvent.PointsValue don't rewrite
/// history for members already credited.</summary>
public sealed class EventAttendance : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public int PointsAwarded { get; private set; }
    public DateTime AwardedAt { get; private set; }
    public Guid AwardedByUserId { get; private set; }

    public MemberEvent Event { get; private set; } = null!;
    public User User { get; private set; } = null!;

    private EventAttendance() { }

    public EventAttendance(Guid id, Guid tenantId, Guid eventId, Guid userId, int pointsAwarded, Guid awardedByUserId)
    {
        Id = id;
        TenantId = tenantId;
        EventId = eventId;
        UserId = userId;
        PointsAwarded = pointsAwarded;
        AwardedByUserId = awardedByUserId;
        AwardedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }
}
