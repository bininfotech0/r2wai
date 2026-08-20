using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class PlanUpgradeRequest : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public PlanTier RequestedTier { get; private set; }
    public string? PaymentReference { get; private set; }
    public PlanUpgradeRequestStatus Status { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }

    public User User { get; private set; } = null!;

    private PlanUpgradeRequest() { }

    public PlanUpgradeRequest(Guid id, Guid tenantId, Guid userId, PlanTier requestedTier, string? paymentReference = null)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        RequestedTier = requestedTier;
        PaymentReference = paymentReference;
        Status = PlanUpgradeRequestStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Approve(Guid reviewedByUserId)
    {
        if (Status != PlanUpgradeRequestStatus.Pending)
            throw new InvalidOperationException($"Plan upgrade request is already {Status}.");
        Status = PlanUpgradeRequestStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void Reject(Guid reviewedByUserId)
    {
        if (Status != PlanUpgradeRequestStatus.Pending)
            throw new InvalidOperationException($"Plan upgrade request is already {Status}.");
        Status = PlanUpgradeRequestStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTime.UtcNow;
        MarkAsModified();
    }
}
