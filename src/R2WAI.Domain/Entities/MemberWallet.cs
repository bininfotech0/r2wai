using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>
/// Internal ledger only — no real money moves through this entity. WalletBalanceInRupees is
/// bookkeeping; actual payouts happen off-system via a WithdrawalRequest an admin fulfills
/// manually (bank transfer, UPI, etc.) and then marks Completed. See the plan's compliance note.
/// </summary>
public sealed class MemberWallet : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public int PointsBalance { get; private set; }
    public decimal WalletBalanceInRupees { get; private set; }
    public string ReferralCode { get; private set; }
    public Guid? ReferredByUserId { get; private set; }
    public PlanTier PlanTier { get; private set; } = PlanTier.Free;

    public User User { get; private set; } = null!;

    private MemberWallet() { }

    public MemberWallet(Guid id, Guid tenantId, Guid userId, string referralCode, Guid? referredByUserId = null)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        ReferralCode = referralCode;
        ReferredByUserId = referredByUserId;
        PointsBalance = 0;
        WalletBalanceInRupees = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public void AddPoints(int points)
    {
        if (points <= 0) throw new InvalidOperationException("Points to add must be positive.");
        PointsBalance += points;
        MarkAsModified();
    }

    public void ConvertPointsToWallet(int points, decimal amount)
    {
        if (points <= 0) throw new InvalidOperationException("Points to convert must be positive.");
        if (points > PointsBalance) throw new InvalidOperationException("Insufficient points balance.");
        PointsBalance -= points;
        WalletBalanceInRupees += amount;
        MarkAsModified();
    }

    public void HoldForWithdrawal(decimal amount)
    {
        if (amount <= 0) throw new InvalidOperationException("Withdrawal amount must be positive.");
        if (amount > WalletBalanceInRupees) throw new InvalidOperationException("Insufficient wallet balance.");
        WalletBalanceInRupees -= amount;
        MarkAsModified();
    }

    public void ReleaseHold(decimal amount)
    {
        WalletBalanceInRupees += amount;
        MarkAsModified();
    }

    public void UpgradePlan(PlanTier tier)
    {
        PlanTier = tier;
        MarkAsModified();
    }
}
