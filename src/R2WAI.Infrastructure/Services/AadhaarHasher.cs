using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.Services;

/// <summary>
/// HMAC-SHA256 keyed with the same secret as <see cref="EncryptionService"/> (ENCRYPTION_KEY) — see
/// IAadhaarHasher's doc comment for why a plain unsalted hash isn't safe here. Reuses
/// EncryptionService's exact key-loading/validation logic rather than sharing a dependency on it, so
/// this stays independently constructible in DI without coupling the two services' lifetimes.
/// </summary>
public class AadhaarHasher : IAadhaarHasher
{
    private readonly byte[] _key;

    public AadhaarHasher(IConfiguration configuration, IHostEnvironment environment)
    {
        var keyFromEnv = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
        var keyFromConfig = configuration["Security:EncryptionKey"];

        var keyString = keyFromEnv ?? keyFromConfig
            ?? throw new ConfigurationException(
                "Encryption key not configured. Set the ENCRYPTION_KEY environment variable (32-byte base64 string).");

        if (!environment.IsDevelopment() && keyFromEnv is null && keyFromConfig is not null)
            throw new ConfigurationException(
                "In non-development environments, the encryption key must be supplied via the ENCRYPTION_KEY environment variable, not appsettings.json.");

        _key = Convert.FromBase64String(keyString);
        if (_key.Length != 32)
            throw new ConfigurationException("Encryption key must be exactly 32 bytes (256 bits).");
    }

    public string Hash(string aadhaarNumber)
    {
        if (string.IsNullOrEmpty(aadhaarNumber))
            return string.Empty;

        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(aadhaarNumber));
        return Convert.ToBase64String(hash);
    }
}
