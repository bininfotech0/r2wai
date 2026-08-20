namespace R2WAI.Application.Features.Members.DTOs;

public class MemberWalletDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public int PointsBalance { get; init; }
    public decimal WalletBalanceInRupees { get; init; }
    public string ReferralCode { get; init; } = string.Empty;
    public Guid? ReferredByUserId { get; init; }
    public string PlanTier { get; init; } = "Free";
}
