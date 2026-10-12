using R2WAI.Application.Common.Security;
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

    // Blueprint test matrix (docs/architecture/R2WAI-v0.7.3-BLUEPRINT.md §2.1). Rows marked "was a
    // gap" failed against one or both of the two detectors this one replaced.
    [Theory]
    [InlineData("P-A1", "UID 2345 6789 0123", "Aadhaar")]
    [InlineData("P-A2", "UID 2345-6789-0123", "Aadhaar")]             // was a gap (ingest scanner)
    [InlineData("P-A3", "UID 2345.6789.0123", "Aadhaar")]             // was a gap
    [InlineData("P-A4", "UID 2345 6789 0123", "Aadhaar")]
    [InlineData("P-A5", "UID २३४५ ६७८९ ०१२३", "Aadhaar")]
    [InlineData("P-A6", "ref 123456789012345", null)]                  // 15 digits: not Aadhaar
    [InlineData("P-A7", "UID 2345​6789​0123", "Aadhaar")]   // was a gap (zero-width)
    [InlineData("P-P1", "PAN ABCDE1234F", "PAN")]
    [InlineData("P-P2", "pan abcde1234f", "PAN")]                      // was a gap (ingest scanner)
    [InlineData("P-P3", "PAN ABCDE 1234 F", "PAN")]                    // was a gap
    [InlineData("P-M1", "call +91 9876543210", "Phone")]
    [InlineData("P-M2", "call +91 98765 43210", "Phone")]              // was a gap
    [InlineData("P-M3", "call 09876543210", "Phone")]                  // was a gap
    [InlineData("P-M5", "id 5876543210", null)]                        // must start 6-9
    public void Find_Matrix(string id, string text, string? expectedType)
    {
        var types = PiiDetector.FindTypes(text);
        if (expectedType is null)
            Assert.True(types.Count == 0, $"{id}: false positive [{string.Join(",", types)}]");
        else
            Assert.True(types.Contains(expectedType), $"{id}: expected {expectedType}, got [{string.Join(",", types)}]");
    }

    [Theory]
    [InlineData("call +91 98765 43210 today", "98765")]
    [InlineData("UID 2345​6789​0123", "0123")]
    [InlineData("919876543210", "9876543210")]
    [InlineData("PAN ABCDE 1234 F", "1234")]
    public void Redact_LeavesNoFragmentOfTheIdentifier(string text, string fragment)
    {
        Assert.DoesNotContain(fragment, PiiDetector.Redact(text));
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
