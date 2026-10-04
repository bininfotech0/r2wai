using R2WAI.Application.Common.Security;

namespace R2WAI.Application.Tests.Common;

public class AuthPolicyEvaluatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("All staff must enable MFA before Q3.")] // prose — the common case
    [InlineData("{\"someOtherField\":\"value\"}")] // valid JSON, but not the expected shape
    [InlineData("{not valid json")]
    public void TryParseRequireMfa_NonStructuredOrUnrelatedContent_ReturnsFalse(string? content)
    {
        Assert.False(AuthPolicyEvaluator.TryParseRequireMfa(content));
    }

    [Fact]
    public void TryParseRequireMfa_StructuredTrue_ReturnsTrue()
    {
        Assert.True(AuthPolicyEvaluator.TryParseRequireMfa("{\"requireMfa\":true}"));
    }

    [Fact]
    public void TryParseRequireMfa_StructuredFalse_ReturnsFalse()
    {
        Assert.False(AuthPolicyEvaluator.TryParseRequireMfa("{\"requireMfa\":false}"));
    }

    [Fact]
    public void TryParseRequireMfa_BothFieldsPresent_ReadsOnlyRequireMfa()
    {
        Assert.True(AuthPolicyEvaluator.TryParseRequireMfa("{\"requireMfa\":true,\"maxPasswordAgeDays\":90}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Passwords must be rotated quarterly.")] // prose — the common case
    [InlineData("{\"someOtherField\":\"value\"}")] // valid JSON, but not the expected shape
    [InlineData("{not valid json")]
    [InlineData("{\"maxPasswordAgeDays\":0}")] // non-positive — treated as unconfigured
    [InlineData("{\"maxPasswordAgeDays\":-30}")]
    [InlineData("{\"maxPasswordAgeDays\":\"90\"}")] // string, not a JSON number
    public void TryParseMaxPasswordAgeDays_NonStructuredOrInvalidContent_ReturnsNull(string? content)
    {
        Assert.Null(AuthPolicyEvaluator.TryParseMaxPasswordAgeDays(content));
    }

    [Fact]
    public void TryParseMaxPasswordAgeDays_ValidJson_ReturnsConfiguredValue()
    {
        Assert.Equal(90, AuthPolicyEvaluator.TryParseMaxPasswordAgeDays("{\"maxPasswordAgeDays\":90}"));
    }

    [Fact]
    public void TryParseMaxPasswordAgeDays_BothFieldsPresent_ReadsOnlyMaxPasswordAgeDays()
    {
        Assert.Equal(90, AuthPolicyEvaluator.TryParseMaxPasswordAgeDays("{\"requireMfa\":true,\"maxPasswordAgeDays\":90}"));
    }
}
