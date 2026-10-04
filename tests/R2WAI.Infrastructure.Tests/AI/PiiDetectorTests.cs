using R2WAI.Infrastructure.AI.Policies;

namespace R2WAI.Infrastructure.Tests.AI;

public class PiiDetectorTests
{
    [Theory]
    [InlineData("My Aadhaar is 1234 5678 9012", "Aadhaar")]
    [InlineData("Aadhaar: 123456789012", "Aadhaar")]
    [InlineData("PAN number ABCDE1234F on file", "PAN")]
    [InlineData("reach me at jane.doe@example.com", "Email")]
    [InlineData("call 9876543210 or +91 9876543210", "Phone")]
    public void Find_DetectsExpectedType(string text, string expectedType)
    {
        var matches = PiiDetector.Find(text);
        Assert.Contains(matches, m => m.Type == expectedType);
    }

    [Theory]
    [InlineData("Just a normal sentence with no personal data.")]
    [InlineData("The meeting is scheduled for 3pm on floor 6.")]
    [InlineData("")]
    public void Find_NoFalsePositivesOnPlainText(string text)
    {
        Assert.Empty(PiiDetector.Find(text));
    }

    [Fact]
    public void ContainsPii_TrueWhenAnyMatchExists()
    {
        Assert.True(PiiDetector.ContainsPii("Aadhaar 123456789012"));
        Assert.False(PiiDetector.ContainsPii("no pii here"));
    }

    [Fact]
    public void Redact_ReplacesAadhaarWithLabeledPlaceholder()
    {
        var redacted = PiiDetector.Redact("My Aadhaar is 123456789012, thanks.");
        Assert.DoesNotContain("123456789012", redacted);
        Assert.Contains("[REDACTED:AADHAAR]", redacted);
    }

    [Fact]
    public void Redact_ReplacesMultipleDistinctTypes()
    {
        var redacted = PiiDetector.Redact("Email me at jane@example.com or call 9876543210.");
        Assert.DoesNotContain("jane@example.com", redacted);
        Assert.DoesNotContain("9876543210", redacted);
        Assert.Contains("[REDACTED:EMAIL]", redacted);
        Assert.Contains("[REDACTED:PHONE]", redacted);
    }

    [Fact]
    public void Redact_LeavesPlainTextUnchanged()
    {
        const string text = "Nothing sensitive in this message at all.";
        Assert.Equal(text, PiiDetector.Redact(text));
    }
}

public class PiiPolicyEvaluatorTests
{
    [Theory]
    [InlineData("{\"action\":\"redact\"}", "redact")]
    [InlineData("{\"action\":\"REDACT\"}", "redact")]
    [InlineData("{\"action\":\"block\"}", "block")]
    public void TryParseAction_RecognizedStructuredContent_ReturnsAction(string content, string expected)
    {
        Assert.Equal(expected, PiiPolicyEvaluator.TryParseAction(content));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Describe the rule this policy enforces...")]
    [InlineData("{\"action\":\"delete-everything\"}")]
    [InlineData("{\"notAction\":\"redact\"}")]
    [InlineData("not json at all {")]
    public void TryParseAction_ProseOrUnrecognizedContent_ReturnsNull(string? content)
    {
        Assert.Null(PiiPolicyEvaluator.TryParseAction(content));
    }
}
