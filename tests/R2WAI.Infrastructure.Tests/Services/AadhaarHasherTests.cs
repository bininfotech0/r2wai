using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace R2WAI.Infrastructure.Tests.Services;

/// <summary>
/// Unsalted Aadhaar hash finding from the 2026-09-20 audit: AadhaarNumberHash used to be a plain
/// SHA-256 of the raw number, brute-forceable offline (~10^11 possibilities, per AadhaarValidator) if
/// the hash column ever leaked. This proves the replacement is both deterministic (still usable for
/// the exact-match duplicate-detection lookup) and actually keyed (not reproducible without the
/// secret) — see IAadhaarHasher's doc comment for the full reasoning.
/// </summary>
public class AadhaarHasherTests
{
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static AadhaarHasher CreateHasher(string? key = null)
    {
        var actualKey = key ?? Convert.ToBase64String(new byte[32]);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:EncryptionKey"] = actualKey })
            .Build();

        return new AadhaarHasher(config, new DevelopmentEnvironment());
    }

    [Fact]
    public void Hash_SameInputSameKey_IsDeterministic()
    {
        var hasher = CreateHasher();

        var hash1 = hasher.Hash("234567890123");
        var hash2 = hasher.Hash("234567890123");

        // Duplicate-detection depends on this: the same Aadhaar number must always hash to the same
        // value so a second registration attempt can be found by an exact-match lookup.
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_DifferentInputs_ProduceDifferentHashes()
    {
        var hasher = CreateHasher();

        var hash1 = hasher.Hash("234567890123");
        var hash2 = hasher.Hash("234567890124");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_IsKeyed_DifferentKeysProduceDifferentHashesForTheSameInput()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        key1[0] = 1;
        key2[0] = 2;
        var hasherA = CreateHasher(Convert.ToBase64String(key1));
        var hasherB = CreateHasher(Convert.ToBase64String(key2));

        var hashA = hasherA.Hash("234567890123");
        var hashB = hasherB.Hash("234567890123");

        // The actual point of this fix: knowing the hash column alone (without the app's own key)
        // isn't enough to correlate or brute-force it — a different key gives a wholly different hash
        // for the identical Aadhaar number.
        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void Hash_DoesNotMatchAPlainUnkeyedSha256OfTheSameInput()
    {
        var hasher = CreateHasher();
        var input = "234567890123";

        var keyedHash = hasher.Hash(input);
        var plainSha256 = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)));

        // Regression guard for the actual vulnerability: this must not degrade back into the old
        // unsalted SHA-256 scheme.
        Assert.NotEqual(plainSha256, keyedHash);
    }

    [Fact]
    public void Hash_OutputIsNotTheRawInput()
    {
        var hasher = CreateHasher();

        var hash = hasher.Hash("234567890123");

        Assert.NotEqual("234567890123", hash);
        Assert.NotEmpty(hash);
    }
}
