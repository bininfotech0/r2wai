namespace R2WAI.Application.Features.Members.DTOs;

public class PlanUpgradeRequestDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string MemberName { get; init; } = string.Empty;
    public string RequestedTier { get; init; } = string.Empty;
    public string? PaymentReference { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? ReviewedAt { get; init; }
    public Guid? ReviewedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
}
