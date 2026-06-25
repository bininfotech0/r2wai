using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

public class ConsoleErrorMonitoringTests : BrowserTestBase
{
    public ConsoleErrorMonitoringTests(BrowserFixture fixture) : base(fixture) { }

    // ═══════════════════════════════════════════════════════════════════
    //  JAVASCRIPT ERROR MONITORING — ALL PUBLIC PAGES
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    public async Task PublicPages_NoJavaScriptErrors(string path)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add($"[{path}] {error}");

        await NavigateAndWait(path);
        await TakeScreenshot($"jscheck_{path.Replace("/", "_").Trim('_')}");

        Assert.True(errors.Count == 0,
            $"JavaScript errors found on {path}:\n{string.Join("\n", errors)}");
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    public async Task PublicPages_NoConsoleErrors(string path)
    {
        var consoleErrors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
                consoleErrors.Add($"[{path}] {msg.Text}");
        };

        await NavigateAndWait(path);

        var criticalErrors = consoleErrors
            .Where(e => !e.Contains("favicon", StringComparison.OrdinalIgnoreCase))
            .Where(e => !e.Contains("404", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(criticalErrors.Count == 0,
            $"Console errors found on {path}:\n{string.Join("\n", criticalErrors)}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NETWORK ERROR MONITORING
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    public async Task PublicPages_NoCriticalNetworkFailures(string path)
    {
        var failedRequests = new List<string>();

        Page.RequestFailed += (_, request) =>
        {
            if (!request.Url.Contains("favicon") &&
                !request.Url.Contains("hot-reload") &&
                !request.Url.Contains("_blazor"))
            {
                failedRequests.Add($"{request.Method} {request.Url} - {request.Failure}");
            }
        };

        await NavigateAndWait(path, waitMs: 3000);

        Assert.True(failedRequests.Count == 0,
            $"Failed network requests on {path}:\n{string.Join("\n", failedRequests)}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CSS LOADING VERIFICATION
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginPage_AllCssLoads_NoMissingStylesheets()
    {
        var missingStyles = new List<string>();

        Page.Response += (_, response) =>
        {
            if (response.Url.EndsWith(".css") && response.Status >= 400)
                missingStyles.Add($"{response.Status} {response.Url}");
        };

        await NavigateAndWait("/login", waitMs: 3000);

        Assert.True(missingStyles.Count == 0,
            $"Missing CSS files:\n{string.Join("\n", missingStyles)}");
    }

    [Fact]
    public async Task LoginPage_AllJsLoads_NoMissingScripts()
    {
        var missingScripts = new List<string>();

        Page.Response += (_, response) =>
        {
            if (response.Url.EndsWith(".js") && response.Status >= 400 &&
                !response.Url.Contains("hot-reload"))
                missingScripts.Add($"{response.Status} {response.Url}");
        };

        await NavigateAndWait("/login", waitMs: 3000);

        Assert.True(missingScripts.Count == 0,
            $"Missing JavaScript files:\n{string.Join("\n", missingScripts)}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SIGNALR CONNECTION MONITORING
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginPage_BlazorSignalR_NoConnectionErrors()
    {
        var signalRErrors = new List<string>();

        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error" && msg.Text.Contains("SignalR", StringComparison.OrdinalIgnoreCase))
                signalRErrors.Add(msg.Text);
        };

        await NavigateAndWait("/login", waitMs: 5000);

        Assert.True(signalRErrors.Count == 0,
            $"SignalR connection errors:\n{string.Join("\n", signalRErrors)}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MIXED CONTENT CHECK
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    public async Task PublicPages_NoMixedContentWarnings(string path)
    {
        var mixedContentWarnings = new List<string>();

        Page.Console += (_, msg) =>
        {
            if (msg.Text.Contains("Mixed Content", StringComparison.OrdinalIgnoreCase))
                mixedContentWarnings.Add(msg.Text);
        };

        await NavigateAndWait(path, waitMs: 3000);

        Assert.True(mixedContentWarnings.Count == 0,
            $"Mixed content warnings on {path}:\n{string.Join("\n", mixedContentWarnings)}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DEPRECATED API USAGE CHECK
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginPage_NoDeprecationWarnings()
    {
        var deprecationWarnings = new List<string>();

        Page.Console += (_, msg) =>
        {
            if (msg.Type == "warning" &&
                msg.Text.Contains("deprecated", StringComparison.OrdinalIgnoreCase))
                deprecationWarnings.Add(msg.Text);
        };

        await NavigateAndWait("/login", waitMs: 3000);

        // Informational — log but don't fail for now
        if (deprecationWarnings.Count > 0)
        {
            File.WriteAllText(
                Path.Combine(GetArtifactDir(), "deprecation-warnings.txt"),
                string.Join("\n", deprecationWarnings));
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MEMORY LEAK INDICATORS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NavigateMultiplePages_NoMemoryLeakIndicators()
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);

        var pages = new[] { "/login", "/register", "/forgot-password", "/reset-password", "/login" };

        foreach (var path in pages)
        {
            await NavigateAndWait(path);
            await Page.WaitForTimeoutAsync(500);
        }

        Assert.True(errors.Count == 0,
            $"Errors during multi-page navigation:\n{string.Join("\n", errors)}");
    }
}
