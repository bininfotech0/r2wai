using R2WAI.Infrastructure.AI;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers ToolExecutionPolicyEvaluator — the pure parse/compare logic behind the optional
/// "ToolExecution" GlobalPolicy risk ceiling. The critical property under test throughout is
/// backward compatibility: anything that isn't a clean {"maxRiskLevel":"..."} JSON object (prose
/// text, empty content, no policy, an unrecognized label) must resolve to "no ceiling" so a tenant
/// that never touches this feature sees byte-identical behavior to before the Policy Engine existed.
/// </summary>
public class ToolExecutionPolicyEvaluatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Only high-risk tools require dual approval.")] // prose — the common, expected case
    [InlineData("{\"someOtherField\":\"value\"}")] // valid JSON, but not the expected shape
    [InlineData("{not valid json")]
    public void TryParseMaxRiskLevel_NonStructuredOrUnrelatedContent_ReturnsNull(string? content)
    {
        Assert.Null(ToolExecutionPolicyEvaluator.TryParseMaxRiskLevel(content));
    }

    [Fact]
    public void TryParseMaxRiskLevel_ValidJson_ReturnsConfiguredValue()
    {
        var result = ToolExecutionPolicyEvaluator.TryParseMaxRiskLevel("{\"maxRiskLevel\":\"Medium\"}");
        Assert.Equal("Medium", result);
    }

    [Theory]
    [InlineData("Low", null, false)] // no ceiling configured -> never blocks
    [InlineData("High", null, false)]
    [InlineData("Low", "Low", false)]
    [InlineData("Medium", "Low", true)]
    [InlineData("High", "Low", true)]
    [InlineData("High", "Medium", true)]
    [InlineData("Medium", "Medium", false)]
    [InlineData("Low", "High", false)]
    [InlineData("Medium", "High", false)]
    public void ExceedsCeiling_ComparesRiskRankCorrectly(string toolRisk, string? maxRisk, bool expected)
    {
        Assert.Equal(expected, ToolExecutionPolicyEvaluator.ExceedsCeiling(toolRisk, maxRisk));
    }

    [Theory]
    [InlineData("Critical", "Low")] // unrecognized tool risk label -> don't block
    [InlineData("High", "Critical")] // unrecognized ceiling label -> don't block
    public void ExceedsCeiling_UnrecognizedLabel_NeverBlocks(string toolRisk, string maxRisk)
    {
        Assert.False(ToolExecutionPolicyEvaluator.ExceedsCeiling(toolRisk, maxRisk));
    }

    [Fact]
    public void ExceedsCeiling_IsCaseInsensitive()
    {
        Assert.True(ToolExecutionPolicyEvaluator.ExceedsCeiling("high", "low"));
        Assert.False(ToolExecutionPolicyEvaluator.ExceedsCeiling("low", "HIGH"));
    }
}
