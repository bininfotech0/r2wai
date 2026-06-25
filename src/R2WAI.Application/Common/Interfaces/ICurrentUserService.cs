namespace R2WAI.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string[] Roles { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
}
