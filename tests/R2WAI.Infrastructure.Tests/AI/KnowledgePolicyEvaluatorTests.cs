using R2WAI.Infrastructure.AI.Policies;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers KnowledgePolicyEvaluator — the pure parse/compare logic behind the optional "Knowledge"
/// GlobalPolicy data-classification ceiling. Mirrors ToolExecutionPolicyEvaluatorTests: anything that
/// isn't a clean {"maxClassification":"..."} JSON object (prose text, empty content, no policy, an
/// unrecognized label) must resolve to "no ceiling" so a tenant that never touches this feature sees
/// byte-identical behavior to before the Policy Engine existed.
/// </summary>
public class KnowledgePolicyEvaluatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Restricted knowledge bases require legal sign-off before RAG use.")] // prose — the common case
    [InlineData("{\"someOtherField\":\"value\"}")] // valid JSON, but not the expected shape
    [InlineData("{not valid json")]
    public void TryParseMaxClassification_NonStructuredOrUnrelatedContent_ReturnsNull(string? content)
    {
        Assert.Null(KnowledgePolicyEvaluator.TryParseMaxClassification(content));
    }

    [Fact]
    public void TryParseMaxClassification_ValidJson_ReturnsConfiguredValue()
    {
        var result = KnowledgePolicyEvaluator.TryParseMaxClassification("{\"maxClassification\":\"Confidential\"}");
        Assert.Equal("Confidential", result);
    }

    [Theory]
    [InlineData("Public", null, false)] // no ceiling configured -> never blocks
    [InlineData("Restricted", null, false)]
    [InlineData("Public", "Public", false)]
    [InlineData("Internal", "Public", true)]
    [InlineData("Restricted", "Public", true)]
    [InlineData("Restricted", "Confidential", true)]
    [InlineData("Confidential", "Confidential", false)]
    [InlineData("Public", "Restricted", false)]
    [InlineData("Internal", "Restricted", false)]
    public void ExceedsCeiling_ComparesClassificationRankCorrectly(string kbClassification, string? maxClassification, bool expected)
    {
        Assert.Equal(expected, KnowledgePolicyEvaluator.ExceedsCeiling(kbClassification, maxClassification));
    }

    [Theory]
    [InlineData("TopSecret", "Public")] // unrecognized kb classification -> don't block
    [InlineData("Restricted", "TopSecret")] // unrecognized ceiling label -> don't block
    public void ExceedsCeiling_UnrecognizedLabel_NeverBlocks(string kbClassification, string maxClassification)
    {
        Assert.False(KnowledgePolicyEvaluator.ExceedsCeiling(kbClassification, maxClassification));
    }

    [Fact]
    public void ExceedsCeiling_IsCaseInsensitive()
    {
        Assert.True(KnowledgePolicyEvaluator.ExceedsCeiling("restricted", "public"));
        Assert.False(KnowledgePolicyEvaluator.ExceedsCeiling("public", "RESTRICTED"));
    }
}
