using R2WAI.Application.Features.Chatbots;

namespace R2WAI.Application.Tests.Common;

public class ChatbotOriginPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseAllowedOrigins_EmptyContent_ReturnsNull(string? content)
    {
        Assert.Null(ChatbotOriginPolicy.TryParseAllowedOrigins(content));
    }

    [Fact]
    public void TryParseAllowedOrigins_MalformedJson_ReturnsNull()
    {
        Assert.Null(ChatbotOriginPolicy.TryParseAllowedOrigins("{not valid json"));
    }

    [Fact]
    public void TryParseAllowedOrigins_ValidJsonArray_ReturnsOrigins()
    {
        var result = ChatbotOriginPolicy.TryParseAllowedOrigins("[\"https://acme.example\",\"https://widget.acme.example\"]");
        Assert.Equal(["https://acme.example", "https://widget.acme.example"], result);
    }

    [Fact]
    public void IsOriginAllowed_NoRestrictionConfigured_AllowsAnyOrigin()
    {
        Assert.True(ChatbotOriginPolicy.IsOriginAllowed(null, "https://anything.example"));
        Assert.True(ChatbotOriginPolicy.IsOriginAllowed(null, null));
    }

    [Fact]
    public void IsOriginAllowed_RestrictionConfigured_MatchingOrigin_Allowed()
    {
        Assert.True(ChatbotOriginPolicy.IsOriginAllowed(["https://acme.example"], "https://acme.example"));
    }

    [Fact]
    public void IsOriginAllowed_RestrictionConfigured_CaseInsensitiveAndTrailingSlash_Allowed()
    {
        Assert.True(ChatbotOriginPolicy.IsOriginAllowed(["https://ACME.example/"], "https://acme.example"));
    }

    [Fact]
    public void IsOriginAllowed_RestrictionConfigured_NonMatchingOrigin_Denied()
    {
        Assert.False(ChatbotOriginPolicy.IsOriginAllowed(["https://acme.example"], "https://evil.example"));
    }

    [Fact]
    public void IsOriginAllowed_RestrictionConfigured_MissingOriginHeader_Denied()
    {
        Assert.False(ChatbotOriginPolicy.IsOriginAllowed(["https://acme.example"], null));
        Assert.False(ChatbotOriginPolicy.IsOriginAllowed(["https://acme.example"], ""));
    }

    [Fact]
    public void IsOriginAllowed_EmptyAllowlist_DeniesEverything()
    {
        Assert.False(ChatbotOriginPolicy.IsOriginAllowed([], "https://acme.example"));
    }
}
