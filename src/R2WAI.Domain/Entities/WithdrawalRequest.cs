using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>A member's request to cash out their wallet balance. Approving/Completing this does
/// NOT move real money — the admin transfers funds off-system (bank/UPI) and marks Completed to
/// record that it actually happened. See the plan's compliance note for why.</summary>
public sealed class WithdrawalRequest : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal AmountRequested { get; private set; }
    public string PayoutMethod { get; private set; }
    public WithdrawalRequestStatus Status { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? AdminNotes { get; private set; }

    public User User { get; private set; } = null!;

    private WithdrawalRequest() { }

    public WithdrawalRequest(Guid id, Guid tenantId, Guid userId, decimal amountRequested, string payoutMethod)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        AmountRequested = amountRequested;
        PayoutMethod = payoutMethod;
        Status = WithdrawalRequestStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Approve(Guid reviewedByUserId, string? adminNotes = null)
    {
        if (Status != WithdrawalRequestStatus.Pending)
            throw new InvalidOperationException($"Withdrawal request is already {Status}.");
        Status = WithdrawalRequestStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTime.UtcNow;
        AdminNotes = adminNotes;
        MarkAsModified();
    }

    public void Reject(Guid reviewedByUserId, string? adminNotes = null)
    {
        if (Status != WithdrawalRequestStatus.Pending)
            throw new InvalidOperationException($"Withdrawal request is already {Status}.");
        Status = WithdrawalRequestStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = DateTime.UtcNow;
        AdminNotes = adminNotes;
        MarkAsModified();
    }

    public void Complete()
    {
        if (Status != WithdrawalRequestStatus.Approved)
            throw new InvalidOperationException($"Only an approved withdrawal request can be completed (current status: {Status}).");
        Status = WithdrawalRequestStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();
    }
}
