using Microsoft.EntityFrameworkCore;

namespace R2WAI.Infrastructure.Authentication;

public class AuthorizationService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<AuthorizationService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, Permission permission, CancellationToken ct = default)
    {
        try
        {
            var userRoles = await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userId)
                .ToListAsync(ct);

            foreach (var userRole in userRoles)
            {
                if (userRole.Role?.Permissions is null) continue;

                var permissions = userRole.Role.Permissions
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var perm in permissions)
                {
                    if (Enum.TryParse<Permission>(perm, out var parsed) && parsed.HasFlag(permission))
                        return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permissions for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> HasRoleAsync(Guid userId, string roleName, CancellationToken ct = default)
    {
        try
        {
            return await _context.UserRoles
                .Include(ur => ur.Role)
                .AnyAsync(ur => ur.UserId == userId && ur.Role!.Name == roleName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking role for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> AuthorizeTenantAccessAsync(Guid tenantId, CancellationToken ct = default)
    {
        var currentTenantId = _currentUserService.TenantId;
        if (currentTenantId is null) return false;

        return currentTenantId.Value == tenantId;
    }
}
