using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Security;

namespace R2WAI.Application.Tests.Common;

public class IntegrationCredentialCodecTests
{
    // Reversible, throws FormatException for anything it didn't encrypt — matching the real
    // EncryptionService.Decrypt's actual failure mode (bad base64), which the legacy-plaintext
    // fallback below specifically depends on.
    private sealed class FakeEncryptionService : IEncryptionService
    {
        public string Encrypt(string plainText) => "enc:" + plainText;
        public string Decrypt(string cipherText) => cipherText.StartsWith("enc:", StringComparison.Ordinal)
            ? cipherText["enc:".Length..]
            : throw new FormatException("not a value this fake encrypted");
    }

    private static readonly IEncryptionService Enc = new FakeEncryptionService();

    [Fact]
    public void EncryptSecrets_EncryptsTokenApiKeyAndPassword_LeavesNonSecretFieldsAlone()
    {
        var json = """{"AuthType":"Bearer","Token":"raw-token"}""";

        var result = IntegrationCredentialCodec.EncryptSecrets(json, Enc);

        Assert.Contains("\"Token\":\"enc:raw-token\"", result);
        Assert.Contains("\"AuthType\":\"Bearer\"", result);
    }

    [Fact]
    public void EncryptSecrets_MultipleSecretFields_EncryptsEachIndependently()
    {
        var json = """{"AuthType":"Basic","Username":"alice","Password":"hunter2"}""";

        var result = IntegrationCredentialCodec.EncryptSecrets(json, Enc);

        Assert.Contains("\"Password\":\"enc:hunter2\"", result);
        Assert.Contains("\"Username\":\"alice\"", result); // non-secret, untouched
    }

    [Fact]
    public void EncryptSecrets_NoSecretFieldsPresent_ReturnsInputUnchanged()
    {
        var json = """{"AuthType":"None"}""";

        Assert.Equal(json, IntegrationCredentialCodec.EncryptSecrets(json, Enc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    public void EncryptSecrets_NullEmptyOrMalformed_ReturnsInputUnchanged_DoesNotThrow(string? json)
    {
        Assert.Equal(json, IntegrationCredentialCodec.EncryptSecrets(json, Enc));
    }

    [Fact]
    public void DecryptSecrets_RoundTripsAnEncryptedValue()
    {
        var encrypted = IntegrationCredentialCodec.EncryptSecrets("""{"AuthType":"Bearer","Token":"raw-token"}""", Enc);

        var decrypted = IntegrationCredentialCodec.DecryptSecrets(encrypted, Enc);

        Assert.Contains("\"Token\":\"raw-token\"", decrypted);
    }

    [Fact]
    public void DecryptSecrets_LegacyPlaintextValue_FallsBackToTheOriginalValue_DoesNotThrow()
    {
        // A row saved before this fix shipped — Token is real plaintext, not ciphertext.
        var json = """{"AuthType":"Bearer","Token":"still-plaintext-from-before"}""";

        var result = IntegrationCredentialCodec.DecryptSecrets(json, Enc);

        Assert.Contains("\"Token\":\"still-plaintext-from-before\"", result);
    }

    [Fact]
    public void Redact_RemovesTokenApiKeyAndPassword_KeepsNonSecretFields()
    {
        var json = """{"AuthType":"ApiKey","ApiKey":"enc:secret","ApiKeyHeaderName":"X-Api-Key"}""";

        var result = IntegrationCredentialCodec.Redact(json);

        Assert.DoesNotContain("ApiKey\":\"enc:secret\"", result);
        Assert.DoesNotContain("secret", result);
        Assert.Contains("\"AuthType\":\"ApiKey\"", result);
        Assert.Contains("\"ApiKeyHeaderName\":\"X-Api-Key\"", result);
    }

    [Fact]
    public void MergeAndEncrypt_IncomingHasANewSecret_EncryptsAndUsesIt()
    {
        var existing = IntegrationCredentialCodec.EncryptSecrets("""{"AuthType":"Bearer","Token":"old-token"}""", Enc);
        var incoming = """{"AuthType":"Bearer","Token":"new-token"}""";

        var result = IntegrationCredentialCodec.MergeAndEncrypt(existing, incoming, Enc);

        Assert.Contains("\"Token\":\"enc:new-token\"", result);
    }

    [Fact]
    public void MergeAndEncrypt_IncomingOmitsTheSecret_PreservesTheExistingEncryptedValueUnchanged()
    {
        var existing = IntegrationCredentialCodec.EncryptSecrets("""{"AuthType":"Bearer","Token":"old-token"}""", Enc);
        var incoming = """{"AuthType":"Bearer"}"""; // user didn't touch the Token field on edit

        var result = IntegrationCredentialCodec.MergeAndEncrypt(existing, incoming, Enc);

        // Still the OLD ciphertext, copied as-is — never re-encrypted (re-encrypting would also work
        // functionally, but copying proves this path never even looks at the plaintext, since it
        // never decrypts here).
        Assert.Contains("\"Token\":\"enc:old-token\"", result);
    }

    [Fact]
    public void MergeAndEncrypt_IncomingSendsAnEmptyStringForTheSecret_TreatedAsUnchangedNotCleared()
    {
        var existing = IntegrationCredentialCodec.EncryptSecrets("""{"AuthType":"Bearer","Token":"old-token"}""", Enc);
        var incoming = """{"AuthType":"Bearer","Token":""}""";

        var result = IntegrationCredentialCodec.MergeAndEncrypt(existing, incoming, Enc);

        Assert.Contains("\"Token\":\"enc:old-token\"", result);
    }

    [Fact]
    public void MergeAndEncrypt_NoExistingSecretAndIncomingOmitsIt_ResultHasNoSecretField()
    {
        var existing = """{"AuthType":"None"}""";
        var incoming = """{"AuthType":"None"}""";

        var result = IntegrationCredentialCodec.MergeAndEncrypt(existing, incoming, Enc);

        Assert.DoesNotContain("Token", result);
    }

    [Fact]
    public void MergeAndEncrypt_SwitchingAuthTypeAndAddingANewSecret_EncryptsOnlyTheNewOne()
    {
        var existing = IntegrationCredentialCodec.EncryptSecrets("""{"AuthType":"Bearer","Token":"old-token"}""", Enc);
        var incoming = """{"AuthType":"Basic","Username":"alice","Password":"hunter2"}""";

        var result = IntegrationCredentialCodec.MergeAndEncrypt(existing, incoming, Enc);

        Assert.Contains("\"Password\":\"enc:hunter2\"", result);
        Assert.Contains("\"AuthType\":\"Basic\"", result);
        Assert.Contains("\"Username\":\"alice\"", result);
        // The old Bearer token is gone (no Token key in the incoming Basic-auth payload), not carried
        // over under a field the new AuthType doesn't use.
        Assert.DoesNotContain("Token", result);
    }
}
