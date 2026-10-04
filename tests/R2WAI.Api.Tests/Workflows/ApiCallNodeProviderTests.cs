using Elsa.Http;
using Elsa.Workflows.Activities;
using R2WAI.Api.Workflows.NodeProviders;
using R2WAI.Application.Features.Workflows.DTOs;

namespace R2WAI.Api.Tests.Workflows;

/// <summary>
/// P0-9 (2026-09-20 audit), SSRF half: the workflow "API Call" step used Elsa's raw SendHttpRequest
/// directly with no egress guard. ApiCallNodeProvider.CreateActivity is pure/synchronous with no DI
/// dependencies, so this can be tested directly without a live Elsa runtime — unlike a real activity's
/// ExecuteAsync, which this codebase has no established way to test end to end (see the class's own
/// doc comment for why that fuller fix was deliberately not attempted this pass).
/// </summary>
public class ApiCallNodeProviderTests
{
    private static NodeCreationContext CreateContext(string? apiUrl, string? apiMethod = "GET") =>
        new(
            Step: new WorkflowStepDto { Order = 0, Name = "Call Supplier API" },
            WorkflowId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            InstanceId: Guid.NewGuid(),
            Data: null,
            Config: new StepConfigDto { ApiUrl = apiUrl, ApiMethod = apiMethod });

    [Fact]
    public void CreateActivity_PublicUrl_BuildsARealSendHttpRequest()
    {
        var provider = new ApiCallNodeProvider();
        var context = CreateContext("https://api.example.com/orders");

        var activity = provider.CreateActivity(context);

        Assert.IsType<SendHttpRequest>(activity);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")] // cloud metadata endpoint
    [InlineData("http://10.0.0.5/admin")]
    [InlineData("http://localhost:5000/internal")]
    [InlineData(null)] // no URL configured at all
    public void CreateActivity_BlockedOrMissingUrl_DoesNotBuildASendHttpRequest(string? blockedUrl)
    {
        var provider = new ApiCallNodeProvider();
        var context = CreateContext(blockedUrl);

        var activity = provider.CreateActivity(context);

        // The real assertion: whatever this returns, it must not be a live SendHttpRequest that would
        // actually reach the target — a WriteLine placeholder (this project's own established
        // "couldn't build a real activity" convention, see StepActivityFactory's fallback) is fine;
        // what matters is what it is NOT.
        Assert.IsNotType<SendHttpRequest>(activity);
        Assert.IsType<WriteLine>(activity);
    }

    [Fact]
    public void CreateActivity_BlockedUrl_NamesTheStepInThePlaceholder()
    {
        var provider = new ApiCallNodeProvider();
        var context = CreateContext("http://169.254.169.254/latest/meta-data/");

        var activity = provider.CreateActivity(context);

        Assert.Equal("Call Supplier API", activity.Name);
    }
}
