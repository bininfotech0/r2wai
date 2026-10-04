namespace R2WAI.Application.Features.Governance.DTOs;

public class AuthSecurityStatusDto
{
    public int TotalUsers { get; init; }
    public int MfaEnabledUsers { get; init; }
    public bool SsoConfigured { get; init; }
    public bool RequireMfaPolicyActive { get; init; }
}
