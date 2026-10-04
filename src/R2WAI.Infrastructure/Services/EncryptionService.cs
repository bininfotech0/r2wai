using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.Services;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _currentKey;
    private readonly IReadOnlyList<byte[]> _previousKeys;

    public EncryptionService(IConfiguration configuration, IHostEnvironment environment)
    {
        var keyFromEnv = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
        var keyFromConfig = configuration["Security:EncryptionKey"];

        // ConfigurationException, not InvalidOperationException: this is a server misconfiguration,
        // not a client-correctable state conflict. The exception middleware maps InvalidOperationException
        // to 409 Conflict for legitimate domain guards — a missing encryption key must never look like
        // something the caller can fix by retrying, so it falls through to a real 500 instead.
        var keyString = keyFromEnv ?? keyFromConfig
            ?? throw new ConfigurationException(
                "Encryption key not configured. Set the ENCRYPTION_KEY environment variable (32-byte base64 string).");

        if (!environment.IsDevelopment() && keyFromEnv is null && keyFromConfig is not null)
            throw new ConfigurationException(
                "In non-development environments, the encryption key must be supplied via the ENCRYPTION_KEY environment variable, not appsettings.json.");

        _currentKey = ParseKey(keyString, "ENCRYPTION_KEY");

        // Key rotation: retired keys, decrypt-only, never used for new Encrypt calls. Rotating
        // means setting ENCRYPTION_KEY to a freshly generated key and moving the old one here --
        // every existing ciphertext still decrypts (this list is tried after the current key),
        // and every write path that touches a secret field already re-encrypts it under the
        // current key on save, so rows migrate off a retired key naturally over time without a
        // forced bulk rewrite. Intentionally NOT auto-migrated: dropping a retired key from this
        // list is a deliberate operator action, taken once nothing depends on it decrypting
        // anymore (verified out of band -- this class has no way to know that on its own).
        var previousKeysFromEnv = Environment.GetEnvironmentVariable("ENCRYPTION_KEY_PREVIOUS");
        var previousKeysFromConfig = configuration["Security:PreviousEncryptionKeys"];

        if (!environment.IsDevelopment() && previousKeysFromEnv is null && previousKeysFromConfig is not null)
            throw new ConfigurationException(
                "In non-development environments, previous encryption keys must be supplied via the ENCRYPTION_KEY_PREVIOUS environment variable, not appsettings.json.");

        var previousKeysString = previousKeysFromEnv ?? previousKeysFromConfig;
        _previousKeys = string.IsNullOrWhiteSpace(previousKeysString)
            ? []
            : previousKeysString
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select((k, i) => ParseKey(k, $"ENCRYPTION_KEY_PREVIOUS[{i}]"))
                .ToList();
    }

    private static byte[] ParseKey(string keyString, string sourceName)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(keyString);
        }
        catch (FormatException)
        {
            throw new ConfigurationException($"{sourceName} must be a valid base64 string.");
        }

        if (key.Length != 32)
            throw new ConfigurationException($"{sourceName} must decode to exactly 32 bytes (256 bits).");
        return key;
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_currentKey, 16);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var result = new byte[nonce.Length + cipherBytes.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length + cipherBytes.Length, tag.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        var fullBytes = Convert.FromBase64String(cipherText);
        if (fullBytes.Length < 28) // 12 nonce + 0 cipher + 16 tag minimum
            throw new CryptographicException("Invalid cipher text.");

        var nonce = fullBytes[..12];
        var tag = fullBytes[^16..];
        var cipherBytes = fullBytes[12..^16];

        // Tries the current key first (the common case, and the only attempt when no rotation is
        // in progress), then each retired key in registration order. AES-GCM's authentication tag
        // makes trying the wrong key a safe, cheap no-op -- it throws CryptographicException
        // rather than returning garbage -- so no key-id needs to be embedded in the ciphertext and
        // every value ever encrypted stays byte-for-byte decryptable.
        CryptographicException lastFailure = new("Invalid cipher text.");
        foreach (var key in AllKeys())
        {
            try
            {
                var plainBytes = new byte[cipherBytes.Length];
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException ex)
            {
                lastFailure = ex;
            }
        }

        throw lastFailure;
    }

    private IEnumerable<byte[]> AllKeys()
    {
        yield return _currentKey;
        foreach (var key in _previousKeys)
            yield return key;
    }
}
