using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class Notification : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Title { get; private set; }
    public string Message { get; private set; }
    public string Type { get; private set; }
    public string? Link { get; private set; }
    public bool IsRead { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public User? User { get; private set; }

    private Notification() { }

    public Notification(Guid id, Guid tenantId, Guid? userId, string title, string message,
                         string? type = null, string? link = null)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Title = title;
        Message = message;
        Type = type ?? "info";
        Link = link;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkRead()
    {
        IsRead = true;
        MarkAsModified();
    }
}
