namespace R2WAI.Application.Features.Members.DTOs;

public class WithdrawalRequestDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string MemberName { get; init; } = string.Empty;
    public decimal AmountRequested { get; init; }
    public string PayoutMethod { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? ReviewedAt { get; init; }
    public Guid? ReviewedByUserId { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? AdminNotes { get; init; }
    public DateTime CreatedAt { get; init; }
}
