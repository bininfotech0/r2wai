using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class User : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string ExternalId { get; private set; }
    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? PasswordHash { get; private set; }
    public string? RefreshTokenHash { get; private set; }
    public DateTime? RefreshTokenExpiresAt { get; private set; }
    public string? Status { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ICollection<UserRole> UserRoles { get; private set; } = [];
    public ICollection<Conversation> Conversations { get; private set; } = [];

    private User() { }

    public User(Guid id, Guid tenantId, string externalId, string email,
                string firstName, string lastName, string? avatarUrl = null)
    {
        Id = id;
        TenantId = tenantId;
        ExternalId = externalId;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        AvatarUrl = avatarUrl;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string firstName, string lastName, string? avatarUrl)
    {
        FirstName = firstName;
        LastName = lastName;
        AvatarUrl = avatarUrl;
        MarkAsModified();
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        MarkAsModified();
    }

    public void SetLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        MarkAsModified();
    }

    public void SetRefreshToken(string refreshTokenHash, DateTime expiresAt)
    {
        RefreshTokenHash = refreshTokenHash;
        RefreshTokenExpiresAt = expiresAt;
        MarkAsModified();
    }

    public void RevokeRefreshToken()
    {
        RefreshTokenHash = null;
        RefreshTokenExpiresAt = null;
        MarkAsModified();
    }

    public string? PasswordResetToken { get; private set; }
    public DateTime? PasswordResetExpiresAt { get; private set; }

    public bool MfaEnabled { get; private set; }
    public string? MfaSecret { get; private set; }

    public void SetPasswordResetToken(string token, DateTime expiresAt)
    {
        PasswordResetToken = token;
        PasswordResetExpiresAt = expiresAt;
        MarkAsModified();
    }

    public void ClearPasswordResetToken()
    {
        PasswordResetToken = null;
        PasswordResetExpiresAt = null;
        MarkAsModified();
    }

    public void EnableMfa(string secret)
    {
        MfaSecret = secret;
        MfaEnabled = true;
        MarkAsModified();
    }

    public void DisableMfa()
    {
        MfaSecret = null;
        MfaEnabled = false;
        MarkAsModified();
    }

    public bool HasPermission(Permission permission)
    {
        if (UserRoles is null || UserRoles.Count == 0)
            return false;

        return UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.Permissions)
            .Any(permissions => !string.IsNullOrEmpty(permissions) &&
                ParsePermissions(permissions).HasFlag(permission));
    }

    private static Permission ParsePermissions(string permissions)
    {
        var result = Permission.None;
        foreach (var part in permissions.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<Permission>(part, ignoreCase: true, out var parsed))
                result |= parsed;
        }
        return result;
    }
}
