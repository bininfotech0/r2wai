using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class ApiKey : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string KeyHash { get; private set; }
    public string KeyPrefix { get; private set; }
    public string? Scopes { get; private set; }
    public string? Roles { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private ApiKey() { }

    public ApiKey(Guid id, Guid tenantId, string name, string keyHash, string keyPrefix,
                  Guid createdByUserId, string? scopes = null, string? roles = null,
                  DateTime? expiresAt = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        KeyHash = keyHash;
        KeyPrefix = keyPrefix;
        CreatedByUserId = createdByUserId;
        Scopes = scopes;
        Roles = roles;
        ExpiresAt = expiresAt;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string? scopes, string? roles, DateTime? expiresAt)
    {
        Name = name;
        Scopes = scopes;
        Roles = roles;
        ExpiresAt = expiresAt;
        MarkAsModified();
    }

    public void Revoke()
    {
        IsActive = false;
        MarkAsModified();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsModified();
    }

    public void RecordUsage()
    {
        LastUsedAt = DateTime.UtcNow;
    }

    public void Regenerate(string newKeyHash, string newKeyPrefix)
    {
        KeyHash = newKeyHash;
        KeyPrefix = newKeyPrefix;
        MarkAsModified();
    }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}
