using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Application.Common.Security;

/// <summary>
/// Encrypts/decrypts/redacts the secret-bearing fields (Token, ApiKey, Password) embedded inside a
/// ToolDefinition's direct-EndpointUrl Configuration JSON blob — the shape
/// DynamicToolExecutor.ResolveDirectEndpoint and CreateEditIntegrationDialog.tsx both read and write
/// (AuthType/Token/ApiKey/ApiKeyHeaderName/Username/Password). Everything else in the blob (AuthType,
/// ApiKeyHeaderName, Username) is non-secret metadata and passes through unchanged.
///
/// Before this, these fields were stored as plain JSON text AND echoed straight back over
/// GET /api/v1/integrations — the "Edit Integration" dialog literally pre-filled a live Bearer
/// token/password into the form from that response. Same convention ApplicationApi.CredentialSecret
/// already established elsewhere in this codebase: encrypted at rest via IEncryptionService, never
/// returned to the client once saved, "leave blank to keep the current value" on edit.
/// </summary>
public static class IntegrationCredentialCodec
{
    private static readonly string[] SecretKeys = ["Token", "ApiKey", "Password"];
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string? EncryptSecrets(string? configurationJson, IEncryptionService encryptionService) =>
        Transform(configurationJson, (_, value) => encryptionService.Encrypt(value));

    /// <summary>
    /// Decrypts for real dispatch. Falls back to the original value per field on a decrypt failure
    /// (bad base64 / AES-GCM tag) instead of throwing — the only way that happens is a row saved
    /// before this fix shipped, still holding real plaintext from before Configuration was ever
    /// encrypted. Failing the call outright would break every existing integration with a credential
    /// the moment this ships, for something that was never broken before; a legacy row self-heals the
    /// next time anyone re-saves it (Update re-encrypts through MergeAndEncrypt).
    /// </summary>
    public static string? DecryptSecrets(string? configurationJson, IEncryptionService encryptionService, ILogger? logger = null) =>
        Transform(configurationJson, (key, value) =>
        {
            try
            {
                return encryptionService.Decrypt(value);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
                logger?.LogWarning(
                    "Integration Configuration field '{Key}' did not decrypt as ciphertext — treating as legacy plaintext saved before encryption was added", key);
                return value;
            }
        });

    public static string? Redact(string? configurationJson) =>
        Transform(configurationJson, (_, _) => null);

    /// <summary>
    /// Used on update: non-secret fields, and any secret field the caller actually supplied a new,
    /// non-empty value for, come from <paramref name="incomingConfigurationJson"/> (secrets get
    /// encrypted here). A secret field the incoming payload left blank/absent is preserved unchanged
    /// (still encrypted, copied as-is — never re-encrypted) from
    /// <paramref name="existingConfigurationJson"/>.
    /// </summary>
    public static string? MergeAndEncrypt(
        string? existingConfigurationJson, string? incomingConfigurationJson, IEncryptionService encryptionService)
    {
        var incoming = Parse(incomingConfigurationJson);
        if (incoming is null)
            return incomingConfigurationJson; // not a recognised shape — pass through unchanged, same as Transform below

        var existing = Parse(existingConfigurationJson);

        // A blank secret field means "unchanged" only when the auth type itself hasn't changed —
        // switching e.g. Bearer -> Basic makes every secret field from the old type irrelevant, not
        // something to resurrect just because the incoming payload happens to omit it too.
        var sameAuthType = existing is not null
            && existing.TryGetValue("AuthType", out var existingAuthType)
            && incoming.TryGetValue("AuthType", out var incomingAuthType)
            && string.Equals(existingAuthType, incomingAuthType, StringComparison.OrdinalIgnoreCase);

        foreach (var key in SecretKeys)
        {
            if (incoming.TryGetValue(key, out var incomingValue) && !string.IsNullOrEmpty(incomingValue))
            {
                incoming[key] = encryptionService.Encrypt(incomingValue);
                continue;
            }

            incoming.Remove(key);
            if (sameAuthType && existing!.TryGetValue(key, out var existingValue) && !string.IsNullOrEmpty(existingValue))
                incoming[key] = existingValue; // already encrypted from a prior save
        }

        return JsonSerializer.Serialize(incoming, Options);
    }

    private static string? Transform(string? configurationJson, Func<string, string, string?> map)
    {
        var parsed = Parse(configurationJson);
        if (parsed is null)
            return configurationJson; // not a recognised shape (e.g. AuthType: None with no other fields, or malformed) — nothing to transform

        var changed = false;
        foreach (var key in SecretKeys)
        {
            if (!parsed.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
                continue;

            changed = true;
            var mapped = map(key, value);
            if (mapped is null)
                parsed.Remove(key);
            else
                parsed[key] = mapped;
        }

        return changed ? JsonSerializer.Serialize(parsed, Options) : configurationJson;
    }

    private static Dictionary<string, string>? Parse(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(configurationJson, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
