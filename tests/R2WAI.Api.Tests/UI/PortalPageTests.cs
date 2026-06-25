using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

/// <summary>
/// Real browser UI tests for portal navigation.
/// Verifies that protected pages redirect to login and the login page has proper structure.
/// </summary>
public class PortalPageTests : BrowserTestBase
{
    public PortalPageTests(BrowserFixture fixture) : base(fixture) { }
    [Theory]
    [InlineData("/")]
    [InlineData("/assistant-studio")]
    [InlineData("/workflow-studio")]
    [InlineData("/operations")]
    [InlineData("/settings")]
    [InlineData("/workflow-studio/approvals")]
    [InlineData("/operations/audit-logs")]
    [InlineData("/operations/errors")]
    [InlineData("/operations/analytics")]
    [InlineData("/settings/tenant")]
    public async Task ProtectedPages_RedirectToLogin(string path)
    {
        await NavigateAndWait(path);

        // Blazor Server [Authorize] redirects unauthenticated users to /login
        var url = Page.Url;
        var content = await Page.ContentAsync();

        Assert.True(
            url.Contains("/login") ||
            content.Contains("Sign In", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Email", StringComparison.OrdinalIgnoreCase),
            $"Page {path} should redirect to login, got URL: {url}");
    }

    [Fact]
    public async Task LoginPage_HasCorrectStructure()
    {
        await NavigateAndWait("/login");

        // Title
        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);

        // Form elements
        var inputs = await Page.Locator("input").CountAsync();
        Assert.True(inputs >= 2, $"Login page should have at least 2 inputs, found {inputs}");

        var buttons = await Page.Locator("button").CountAsync();
        Assert.True(buttons >= 1, $"Login page should have at least 1 button, found {buttons}");
    }

    [Fact]
    public async Task LoginPage_MudBlazor_ComponentsRender()
    {
        await NavigateAndWait("/login");

        // MudBlazor framework loaded
        var content = await Page.ContentAsync();
        Assert.Contains("mud", content.ToLowerInvariant());

        // MudBlazor CSS is loaded
        var mudCssLoaded = await Page.Locator("link[href*='MudBlazor']").CountAsync();
        Assert.True(mudCssLoaded >= 1, "MudBlazor CSS should be loaded");
    }

    [Fact]
    public async Task LoginPage_BlazorFramework_IsLoaded()
    {
        await NavigateAndWait("/login");

        var content = await Page.ContentAsync();
        Assert.Contains("blazor.server.js", content);
    }

    [Fact]
    public async Task LoginPage_HasR2WAIBranding()
    {
        await NavigateAndWait("/login");

        // Wait for Blazor to render the R2WAI text
        var r2waiText = Page.Locator("text=R2WAI").First;
        await r2waiText.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        Assert.True(await r2waiText.IsVisibleAsync(), "R2WAI branding should be visible");
    }
}
