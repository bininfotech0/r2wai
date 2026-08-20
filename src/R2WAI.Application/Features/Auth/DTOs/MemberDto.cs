namespace R2WAI.Application.Features.Auth.DTOs;

public class MemberDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string? Email { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string MobileNumber { get; init; } = string.Empty;
    public string ReferralCode { get; init; } = string.Empty;
}
