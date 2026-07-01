using System.Security.Claims;

namespace R2WAI.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            return claim is not null && Guid.TryParse(claim.Value, out var userId) ? userId : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id");
            return claim is not null && Guid.TryParse(claim.Value, out var tenantId) ? tenantId : null;
        }
    }

    public string[] Roles
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User?
                .FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToArray() ?? [];
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public string? IpAddress
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null) return null;

            // Only trust X-Forwarded-For when the request arrives from a known private network
            // (i.e. through a trusted reverse proxy). Otherwise use the raw connection IP to
            // prevent clients from forging their address in audit logs.
            var remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp is not null && IsPrivateNetwork(remoteIp))
            {
                var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwarded))
                    return forwarded.Split(',').Last().Trim();
            }

            return remoteIp?.ToString();
        }
    }

    private static bool IsPrivateNetwork(System.Net.IPAddress ip)
    {
        if (System.Net.IPAddress.IsLoopback(ip)) return true;
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false;
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
