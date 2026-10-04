using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A tenant's registered MCP server (implementation plan Phase 3) — one connection can back many
/// <see cref="ToolDefinition"/> rows (one per discovered/committed tool on that server), unlike
/// OpenAPI import, which needs no shared connection record because each imported operation is an
/// independent HTTP call. HTTP transport only, matching what was actually verified against the
/// real ModelContextProtocol client SDK; no stdio/local-process servers.
/// </summary>
public sealed class McpServerConnection : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string EndpointUrl { get; private set; }

    /// <summary>Header name to send the decrypted credential under (e.g. "Authorization"). Null
    /// means the server requires no authentication.</summary>
    public string? AuthHeaderName { get; private set; }

    /// <summary>AES-GCM encrypted (IEncryptionService), same convention as
    /// ApplicationApi.CredentialSecretEncrypted — never returned by any read DTO.</summary>
    public string? CredentialEncrypted { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Same honest three-state signal as ToolDefinition.LastTestStatus — null means
    /// "never tested," not "error."</summary>
    public string? LastTestStatus { get; private set; }
    public DateTime? LastTestedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private McpServerConnection() { }

    public McpServerConnection(Guid id, Guid tenantId, string name, string endpointUrl,
                                string? authHeaderName = null, string? credentialEncrypted = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        EndpointUrl = endpointUrl;
        AuthHeaderName = authHeaderName;
        CredentialEncrypted = credentialEncrypted;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string endpointUrl, string? authHeaderName, string? credentialEncrypted)
    {
        Name = name;
        EndpointUrl = endpointUrl;
        AuthHeaderName = authHeaderName;
        CredentialEncrypted = credentialEncrypted;
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

    public void RecordTestResult(bool success)
    {
        LastTestStatus = success ? "Connected" : "Error";
        LastTestedAt = DateTime.UtcNow;
        MarkAsModified();
    }
}
