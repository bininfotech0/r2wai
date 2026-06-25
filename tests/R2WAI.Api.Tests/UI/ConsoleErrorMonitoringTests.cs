using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

[Collection("Browser")]
public class ConsoleErrorMonitoringTests : BrowserTestBase
{
    public ConsoleErrorMonitoringTests(BrowserFixture fixture) : base(fixture) { }

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    public async Task PublicPages_NoJavaScriptErrors(string path)
    {
        var errors = new List<string>();
        void Handler(object? _, string error) => errors.Add($"[{path}] {error}");
        Page.PageError += Handler;

        try
        {
            await NavigateAndWait(path);
            await TakeScreenshot($"jscheck_{path.Replace("/", "_").Trim('_')}");

            Assert.True(errors.Count == 0,
                $"JavaScript errors found on {path}:\n{string.Join("\n", errors)}");
        }
        finally
        {
            Page.PageError -= Handler;
        }
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    public async Task PublicPages_NoConsoleErrors(string path)
    {
        var consoleErrors = new List<string>();
        void Handler(object? _, IConsoleMessage msg)
        {
            if (msg.Type == "error")
                consoleErrors.Add($"[{path}] {msg.Text}");
        }
        Page.Console += Handler;

        try
        {
            await NavigateAndWait(path);

            var criticalErrors = consoleErrors
                .Where(e => !e.Contains("favicon", StringComparison.OrdinalIgnoreCase))
                .Where(e => !e.Contains("404", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(criticalErrors.Count == 0,
                $"Console errors found on {path}:\n{string.Join("\n", criticalErrors)}");
        }
        finally
        {
            Page.Console -= Handler;
        }
    }

    [Fact]
    public async Task LoginPage_AllCssLoads_NoMissingStylesheets()
    {
        var missingStyles = new List<string>();
        void Handler(object? _, IResponse response)
        {
            if (response.Url.EndsWith(".css") && response.Status >= 400)
                missingStyles.Add($"{response.Status} {response.Url}");
        }
        Page.Response += Handler;

        try
        {
            await NavigateAndWait("/login", waitMs: 3000);

            Assert.True(missingStyles.Count == 0,
                $"Missing CSS files:\n{string.Join("\n", missingStyles)}");
        }
        finally
        {
            Page.Response -= Handler;
        }
    }

    [Fact]
    public async Task LoginPage_AllJsLoads_NoMissingScripts()
    {
        var missingScripts = new List<string>();
        void Handler(object? _, IResponse response)
        {
            if (response.Url.EndsWith(".js") && response.Status >= 400 &&
                !response.Url.Contains("hot-reload"))
                missingScripts.Add($"{response.Status} {response.Url}");
        }
        Page.Response += Handler;

        try
        {
            await NavigateAndWait("/login", waitMs: 3000);

            Assert.True(missingScripts.Count == 0,
                $"Missing JavaScript files:\n{string.Join("\n", missingScripts)}");
        }
        finally
        {
            Page.Response -= Handler;
        }
    }

    [Fact]
    public async Task NavigateMultiplePages_NoErrors()
    {
        var errors = new List<string>();
        void Handler(object? _, string error) => errors.Add(error);
        Page.PageError += Handler;

        try
        {
            var pages = new[] { "/login", "/register", "/forgot-password", "/reset-password", "/login" };

            foreach (var path in pages)
            {
                await NavigateAndWait(path);
                await Page.WaitForTimeoutAsync(500);
            }

            Assert.True(errors.Count == 0,
                $"Errors during multi-page navigation:\n{string.Join("\n", errors)}");
        }
        finally
        {
            Page.PageError -= Handler;
        }
    }
}
