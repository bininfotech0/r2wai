using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class MemberEvent : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public int PointsValue { get; private set; }
    public DateTime EventDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ICollection<EventAttendance> Attendances { get; private set; } = [];

    private MemberEvent() { }

    public MemberEvent(Guid id, Guid tenantId, string name, int pointsValue, DateTime eventDate, string? description = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        PointsValue = pointsValue;
        EventDate = eventDate;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, int pointsValue, DateTime eventDate, string? description)
    {
        Name = name;
        PointsValue = pointsValue;
        EventDate = eventDate;
        Description = description;
        MarkAsModified();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsModified();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsModified();
    }
}
