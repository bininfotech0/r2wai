using R2WAI.Application.Common.AI;

namespace R2WAI.Application.Tests.Common;

public class AiTokenUsageScopeTests
{
    // Stands in for the AI service: reports usage from inside its own awaited async call chain.
    private static async Task CompleteAsync(int tokens)
    {
        await Task.Yield();
        AiTokenUsageScope.Report(tokens);
    }

    [Fact]
    public async Task Report_FromAnAwaitedCall_IsVisibleToTheCallersScope()
    {
        using var usage = AiTokenUsageScope.Begin();

        await CompleteAsync(120);
        await CompleteAsync(30);

        Assert.Equal(150, usage.TotalTokens);
    }

    [Fact]
    public void TotalTokens_IsNull_WhenTheProviderReportedNothing()
    {
        using var usage = AiTokenUsageScope.Begin();

        Assert.Null(usage.TotalTokens);
    }

    [Fact]
    public async Task Report_WithNoOpenScope_IsANoOp_AndClosedScopesStopCollecting()
    {
        await CompleteAsync(10);

        var usage = AiTokenUsageScope.Begin();
        usage.Dispose();
        await CompleteAsync(10);

        Assert.Null(usage.TotalTokens);
    }

    [Fact]
    public async Task NestedScopes_EachSeeTheirOwnShare_AndTheOuterSeesEverything()
    {
        using var outer = AiTokenUsageScope.Begin();
        await CompleteAsync(5);
        using (var inner = AiTokenUsageScope.Begin())
        {
            await CompleteAsync(7);
            Assert.Equal(7, inner.TotalTokens);
        }

        Assert.Equal(12, outer.TotalTokens);
    }
}
