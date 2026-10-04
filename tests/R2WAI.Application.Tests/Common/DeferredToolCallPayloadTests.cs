using R2WAI.Application.Common.Models;

namespace R2WAI.Application.Tests.Common;

public class DeferredToolCallPayloadTests
{
    [Fact]
    public void ToJson_then_TryParse_round_trips()
    {
        var toolId = Guid.NewGuid();
        var payload = new DeferredToolCallPayload(toolId, "{\"orderId\":42}");

        var parsed = DeferredToolCallPayload.TryParse(payload.ToJson());

        Assert.NotNull(parsed);
        Assert.Equal(toolId, parsed!.ToolDefinitionId);
        Assert.Equal("{\"orderId\":42}", parsed.Input);
    }

    [Fact]
    public void ToJson_then_TryParse_round_trips_with_null_input()
    {
        var toolId = Guid.NewGuid();
        var payload = new DeferredToolCallPayload(toolId, null);

        var parsed = DeferredToolCallPayload.TryParse(payload.ToJson());

        Assert.NotNull(parsed);
        Assert.Null(parsed!.Input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_nullOrEmpty_returnsNull(string? json)
    {
        Assert.Null(DeferredToolCallPayload.TryParse(json));
    }

    [Fact]
    public void TryParse_malformedJson_returnsNull_doesNotThrow()
    {
        Assert.Null(DeferredToolCallPayload.TryParse("{not valid json"));
    }

    // A workflow-bound ApprovalRequest's free-text Data (or any other shape that happens to be valid
    // JSON without the "deferred_tool_call" discriminator) must never be misread as a tool call to
    // replay — this is the guard that makes that safe.
    [Fact]
    public void TryParse_validJsonWithoutTheDiscriminator_returnsNull()
    {
        var arbitraryJson = "{\"toolDefinitionId\":\"" + Guid.NewGuid() + "\",\"input\":\"x\"}"; // no "kind"
        Assert.Null(DeferredToolCallPayload.TryParse(arbitraryJson));
    }

    [Fact]
    public void TryParse_plainNonJsonString_returnsNull_doesNotThrow()
    {
        Assert.Null(DeferredToolCallPayload.TryParse("supplier update"));
    }
}
