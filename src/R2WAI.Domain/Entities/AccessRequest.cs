using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class AccessRequest : BaseEntity<Guid>
{
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string Organization { get; private set; }
    public string Department { get; private set; }
    public string? Reason { get; private set; }
    public AccessRequestStatus Status { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public Guid? CreatedUserId { get; private set; }

    private AccessRequest() { }

    public AccessRequest(Guid id, string fullName, string email, string organization, string department, string? reason)
    {
        Id = id;
        FullName = fullName;
        Email = email;
        Organization = organization;
        Department = department;
        Reason = reason;
        Status = AccessRequestStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Approve(Guid reviewedByUserId, Guid createdUserId)
    {
        if (Status != AccessRequestStatus.Pending)
            throw new InvalidOperationException($"Access request is already {Status}.");

        Status = AccessRequestStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        CreatedUserId = createdUserId;
        ReviewedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Reject(Guid reviewedByUserId)
    {
        if (Status != AccessRequestStatus.Pending)
            throw new InvalidOperationException($"Access request is already {Status}.");

        Status = AccessRequestStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTime.UtcNow;
        MarkAsModified();
    }
}
