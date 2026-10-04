using R2WAI.Infrastructure.AI.Policies;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers ApprovalPolicyEvaluator's parsing — the comparison itself is
/// ToolExecutionPolicyEvaluator.ExceedsCeiling, already covered by ToolExecutionPolicyEvaluatorTests,
/// since both levers rank the same Low/Medium/High tool risk scale.
/// </summary>
public class ApprovalPolicyEvaluatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Anything above Low risk needs a manager's sign-off.")] // prose — the common case
    [InlineData("{\"someOtherField\":\"value\"}")] // valid JSON, but not the expected shape
    [InlineData("{not valid json")]
    public void TryParseRequireApprovalAboveRiskLevel_NonStructuredOrUnrelatedContent_ReturnsNull(string? content)
    {
        Assert.Null(ApprovalPolicyEvaluator.TryParseRequireApprovalAboveRiskLevel(content));
    }

    [Fact]
    public void TryParseRequireApprovalAboveRiskLevel_ValidJson_ReturnsConfiguredValue()
    {
        var result = ApprovalPolicyEvaluator.TryParseRequireApprovalAboveRiskLevel("{\"requireApprovalAboveRiskLevel\":\"Medium\"}");
        Assert.Equal("Medium", result);
    }
}
