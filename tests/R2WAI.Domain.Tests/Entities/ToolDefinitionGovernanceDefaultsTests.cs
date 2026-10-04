using R2WAI.Domain.Entities;

namespace R2WAI.Domain.Tests.Entities;

public class ToolDefinitionGovernanceDefaultsTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("get")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void Read_only_methods_are_low_risk_and_need_no_approval(string method)
    {
        var (risk, confirmation, approval) = ToolDefinition.DefaultGovernanceForHttpMethod(method);

        Assert.Equal("Low", risk);
        Assert.False(confirmation);
        Assert.False(approval);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData(" post ")]
    public void Writing_methods_are_medium_risk_and_require_confirmation(string method)
    {
        var (risk, confirmation, approval) = ToolDefinition.DefaultGovernanceForHttpMethod(method);

        Assert.Equal("Medium", risk);
        Assert.True(confirmation);
        Assert.False(approval);
    }

    [Fact]
    public void Delete_is_high_risk_and_requires_approval()
    {
        var (risk, confirmation, approval) = ToolDefinition.DefaultGovernanceForHttpMethod("DELETE");

        Assert.Equal("High", risk);
        Assert.True(confirmation);
        Assert.True(approval);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("TRACE")]
    [InlineData("frobnicate")]
    public void An_unknown_or_missing_method_is_never_treated_as_low_risk(string? method)
    {
        var (risk, _, _) = ToolDefinition.DefaultGovernanceForHttpMethod(method);

        Assert.Equal("Medium", risk);
    }
}
