using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A registered API root for a <see cref="ConnectedApplication"/>. An application may register more
/// than one (e.g. a public API and an internal officer-only API).
/// </summary>
public sealed class ApplicationApi : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Name { get; private set; }
    public string BaseUrl { get; private set; }
    public ApiAuthScheme AuthScheme { get; private set; } = ApiAuthScheme.None;
    public string? CredentialRef { get; private set; }

    // The actual secret, encrypted at rest (IEncryptionService — AES-256-GCM, same mechanism as
    // ModelConfiguration.ApiKeyEncrypted). CredentialRef above stays a human-readable label; this is
    // what DynamicToolExecutor actually resolves and decrypts to make an authenticated call.
    public string? CredentialSecretEncrypted { get; private set; }

    // Only meaningful for AuthScheme.ApiKey — the header the secret gets sent in (e.g. "X-Api-Key").
    // Other schemes use a fixed "Authorization" header, so this stays null for them.
    public string? CredentialHeaderName { get; private set; }
    public string? OpenApiSource { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ConnectedApplication Application { get; private set; } = null!;

    private ApplicationApi() { }

    public ApplicationApi(Guid id, Guid tenantId, Guid applicationId, string name, string baseUrl,
                          ApiAuthScheme authScheme = ApiAuthScheme.None, string? credentialRef = null,
                          string? openApiSource = null)
    {
        Id = id;
        TenantId = tenantId;
        ApplicationId = applicationId;
        Name = name;
        BaseUrl = baseUrl;
        AuthScheme = authScheme;
        CredentialRef = credentialRef;
        OpenApiSource = openApiSource;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string baseUrl, ApiAuthScheme authScheme,
                              string? credentialRef, string? openApiSource)
    {
        Name = name;
        BaseUrl = baseUrl;
        AuthScheme = authScheme;
        CredentialRef = credentialRef;
        OpenApiSource = openApiSource;
        MarkAsModified();
    }

    public void SetCredential(string? credentialSecretEncrypted, string? credentialHeaderName)
    {
        CredentialSecretEncrypted = credentialSecretEncrypted;
        CredentialHeaderName = credentialHeaderName;
        MarkAsModified();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsModified();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsModified();
    }
}
