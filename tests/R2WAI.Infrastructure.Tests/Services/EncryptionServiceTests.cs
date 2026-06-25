using Microsoft.Extensions.Configuration;

namespace R2WAI.Infrastructure.Tests.Services;

public class EncryptionServiceTests
{
    private static EncryptionService CreateService(string? key = null)
    {
        var actualKey = key ?? Convert.ToBase64String(new byte[32]);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:EncryptionKey"] = actualKey
            })
            .Build();

        return new EncryptionService(config);
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
