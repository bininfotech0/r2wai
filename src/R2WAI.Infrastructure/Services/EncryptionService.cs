using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.Services;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(IConfiguration configuration, IHostEnvironment environment)
    {
        var keyFromEnv = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
        var keyFromConfig = configuration["Security:EncryptionKey"];

        var keyString = keyFromEnv ?? keyFromConfig
            ?? throw new InvalidOperationException(
                "Encryption key not configured. Set the ENCRYPTION_KEY environment variable (32-byte base64 string).");

        if (!environment.IsDevelopment() && keyFromEnv is null && keyFromConfig is not null)
            throw new InvalidOperationException(
                "In non-development environments, the encryption key must be supplied via the ENCRYPTION_KEY environment variable, not appsettings.json.");

        _key = Convert.FromBase64String(keyString);
        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption key must be exactly 32 bytes (256 bits).");
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

        using var aes = new AesGcm(_key, 16);
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
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
