using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using R2WAI.Application.Common.Exceptions;
using System.Security.Cryptography;

namespace R2WAI.Infrastructure.Tests.Services;

public class EncryptionServiceTests
{
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static string NewKey(byte marker = 0)
    {
        var key = new byte[32];
        key[0] = marker;
        return Convert.ToBase64String(key);
    }

    private static EncryptionService CreateService(string? key = null, string? previousKeys = null)
    {
        var actualKey = key ?? Convert.ToBase64String(new byte[32]);
        var values = new Dictionary<string, string?> { ["Security:EncryptionKey"] = actualKey };
        if (previousKeys is not null)
            values["Security:PreviousEncryptionKeys"] = previousKeys;

        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new EncryptionService(config, new DevelopmentEnvironment());
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginal()
    {
        var service = CreateService();
        var plaintext = "This is a secret API key: sk-abc123xyz";

        var encrypted = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(encrypted);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachTime()
    {
        var service = CreateService();
        var plaintext = "same-input";

        var encrypted1 = service.Encrypt(plaintext);
        var encrypted2 = service.Encrypt(plaintext);

        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void Encrypt_OutputIsDifferentFromInput()
    {
        var service = CreateService();
        var plaintext = "Hello World";

        var encrypted = service.Encrypt(plaintext);

        Assert.NotEqual(plaintext, encrypted);
        Assert.NotEmpty(encrypted);
    }

    [Fact]
    public void Decrypt_WithWrongKey_Throws()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        key1[0] = 1;
        key2[0] = 2;
        var service1 = CreateService(Convert.ToBase64String(key1));
        var service2 = CreateService(Convert.ToBase64String(key2));

        var encrypted = service1.Encrypt("secret");

        Assert.ThrowsAny<Exception>(() => service2.Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_CiphertextFromRetiredKey_StillSucceeds_ViaPreviousKeysList()
    {
        var oldKey = NewKey(1);
        var newKey = NewKey(2);
        var beforeRotation = CreateService(oldKey);
        var encryptedUnderOldKey = beforeRotation.Encrypt("still needs to work after rotation");

        // Rotation: oldKey moves into PreviousEncryptionKeys, newKey becomes current.
        var afterRotation = CreateService(newKey, previousKeys: oldKey);

        Assert.Equal("still needs to work after rotation", afterRotation.Decrypt(encryptedUnderOldKey));
    }

    [Fact]
    public void Encrypt_AfterRotation_AlwaysUsesCurrentKey_NotAnyPreviousKey()
    {
        var oldKey = NewKey(1);
        var newKey = NewKey(2);
        var afterRotation = CreateService(newKey, previousKeys: oldKey);

        var encrypted = afterRotation.Encrypt("new secret");

        // A service that only knows the retired key must NOT be able to decrypt something
        // encrypted after rotation -- proves Encrypt used the current key, not a previous one.
        var oldKeyOnly = CreateService(oldKey);
        Assert.ThrowsAny<Exception>(() => oldKeyOnly.Decrypt(encrypted));

        // The current-key service (with the same previous key registered) decrypts it fine.
        Assert.Equal("new secret", afterRotation.Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_TriesMultiplePreviousKeysInOrder_UntilOneMatches()
    {
        var key1 = NewKey(1);
        var key2 = NewKey(2);
        var currentKey = NewKey(3);
        var encryptedUnderKey2 = CreateService(key2).Encrypt("from the second retired key");

        var service = CreateService(currentKey, previousKeys: $"{key1},{key2}");

        Assert.Equal("from the second retired key", service.Decrypt(encryptedUnderKey2));
    }

    [Fact]
    public void Constructor_PreviousKeyWithInvalidBase64_ThrowsConfigurationException()
    {
        Assert.Throws<ConfigurationException>(() => CreateService(previousKeys: "not-valid-base64!!"));
    }

    [Fact]
    public void Constructor_PreviousKeyWithWrongLength_ThrowsConfigurationException()
    {
        var shortKey = Convert.ToBase64String(new byte[16]);
        Assert.Throws<ConfigurationException>(() => CreateService(previousKeys: shortKey));
    }

    [Fact]
    public void Constructor_NoPreviousKeysConfigured_DecryptBehaviorUnchanged()
    {
        // Zero previous keys registered -- a wrong-key decrypt must still throw immediately,
        // exactly as it did before key rotation support existed.
        var service1 = CreateService(NewKey(1));
        var service2 = CreateService(NewKey(2));

        var encrypted = service1.Encrypt("secret");

        Assert.ThrowsAny<CryptographicException>(() => service2.Decrypt(encrypted));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("A very long string with special characters: @#$%^&*()_+-=[]{}|;':\",./<>?")]
    [InlineData("Unicode: éàüñ 你好 😀")]
    public void EncryptDecrypt_VariousInputs_Works(string plaintext)
    {
        var service = CreateService();

        var encrypted = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(encrypted);

        Assert.Equal(plaintext, decrypted);
    }
}
