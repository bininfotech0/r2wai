using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

/// <summary>
/// Real browser UI tests for the Login page.
/// Launches the Blazor Server app, opens headless Chromium, and interacts with the rendered page.
/// </summary>
public class LoginPageTests : BrowserTestBase
{
    [Fact]
    public async Task LoginPage_Renders_WithR2WAITitle()
    {
        await NavigateAndWait("/login");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task LoginPage_Shows_EmailField()
    {
        await NavigateAndWait("/login");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await emailInput.IsVisibleAsync(), "Email input should be visible");
    }

    [Fact]
    public async Task LoginPage_Shows_PasswordField()
    {
        await NavigateAndWait("/login");

        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await passwordInput.IsVisibleAsync(), "Password input should be visible");
    }

    [Fact]
    public async Task LoginPage_Shows_SignInButton()
    {
        await NavigateAndWait("/login");

        var button = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await button.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await button.IsVisibleAsync(), "Sign In button should be visible");
    }

    [Fact]
    public async Task LoginPage_CanTypeEmailAndPassword()
    {
        await NavigateAndWait("/login");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await emailInput.FillAsync("test@example.com");

        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.FillAsync("testpassword");

        Assert.Equal("test@example.com", await emailInput.InputValueAsync());
        Assert.Equal("testpassword", await passwordInput.InputValueAsync());
    }

    [Fact]
    public async Task LoginPage_SubmitWithInvalidCredentials_ShowsErrorOrStaysOnPage()
    {
        await NavigateAndWait("/login");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await emailInput.FillAsync("bad@test.com");

        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.FillAsync("wrongpassword");

        var button = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await button.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);

        // Should either show error snackbar or stay on login page
        var url = Page.Url;
        var content = await Page.ContentAsync();
        Assert.True(
            url.Contains("/login") ||
            content.Contains("Invalid", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Connection", StringComparison.OrdinalIgnoreCase),
            "Should show error or stay on login page after invalid credentials");
    }

    [Fact]
    public async Task LoginPage_HasMudBlazorRendered()
    {
        await NavigateAndWait("/login");

        // MudBlazor renders with specific CSS classes
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await mudPaper.IsVisibleAsync(), "MudBlazor paper component should be rendered");
    }
}
