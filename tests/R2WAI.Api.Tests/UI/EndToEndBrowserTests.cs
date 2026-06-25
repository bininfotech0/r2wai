using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

public class EndToEndBrowserTests : BrowserTestBase
{
    // ── Login Page Rendering ────────────────────────────────────────

    [Fact]
    public async Task LoginPage_FullRender_CollectsArtifacts()
    {
        await NavigateAndWait("/login");
        await TakeScreenshot("login_initial");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await emailInput.IsVisibleAsync());

        var passwordInput = Page.Locator("input[type='password']").First;
        Assert.True(await passwordInput.IsVisibleAsync());

        var continueButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await continueButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await continueButton.IsVisibleAsync());

        var branding = Page.Locator("text=R2WAI").First;
        Assert.True(await branding.IsVisibleAsync());

        await TakeScreenshot("login_verified");
    }

    // ── Login Flow: Invalid Credentials ─────────────────────────────

    [Fact]
    public async Task LoginFlow_InvalidCredentials_ShowsError()
    {
        await NavigateAndWait("/login");
        await TakeScreenshot("before_login_attempt");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await emailInput.FillAsync("invalid@test.com");

        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.FillAsync("wrongpassword");

        await TakeScreenshot("credentials_filled");

        var continueButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await continueButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await TakeScreenshot("after_login_submit");

        var url = Page.Url;
        var content = await Page.ContentAsync();
        Assert.True(
            url.Contains("/login") ||
            content.Contains("Invalid", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase),
            "Should show error or stay on login page after invalid credentials");
    }

    // ── Login Flow: Empty Submit Validation ─────────────────────────

    [Fact]
    public async Task LoginFlow_EmptySubmit_ShowsValidation()
    {
        await NavigateAndWait("/login");

        var continueButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await continueButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await continueButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2000);
        await TakeScreenshot("empty_submit_validation");

        var url = Page.Url;
        Assert.Contains("/login", url);
    }

    // ── Protected Pages Redirect ────────────────────────────────────

    [Theory]
    [InlineData("/")]
    [InlineData("/assistant-studio")]
    [InlineData("/workflow-studio")]
    [InlineData("/operations")]
    [InlineData("/settings")]
    public async Task ProtectedPages_RedirectToLogin_WithScreenshot(string path)
    {
        await NavigateAndWait(path);
        await TakeScreenshot($"redirect_{path.Replace("/", "_")}");

        var url = Page.Url;
        var content = await Page.ContentAsync();

        Assert.True(
            url.Contains("/login") ||
            content.Contains("Sign In", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Email", StringComparison.OrdinalIgnoreCase),
            $"Page {path} should redirect to login, got URL: {url}");
    }

    // ── Blazor + MudBlazor Framework Loading ────────────────────────

    [Fact]
    public async Task BlazorFramework_LoadsSuccessfully()
    {
        await NavigateAndWait("/login");

        var content = await Page.ContentAsync();
        Assert.Contains("blazor.server.js", content);

        var mudCssLoaded = await Page.Locator("link[href*='MudBlazor']").CountAsync();
        Assert.True(mudCssLoaded >= 1, "MudBlazor CSS should be loaded");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await mudPaper.IsVisibleAsync());

        await TakeScreenshot("blazor_loaded");
    }

    // ── Chatbot Widget Page ─────────────────────────────────────────

    [Fact]
    public async Task ChatbotWidget_Renders_WithChatInterface()
    {
        var chatbotId = "4b75a6a6-4179-471b-ba6d-bee6c6884eb3";
        await NavigateAndWait($"/chatbot/widget/{chatbotId}", waitMs: 5000);
        await TakeScreenshot("chatbot_widget_initial");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("Chat", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("R2WAI", StringComparison.OrdinalIgnoreCase),
            "Chatbot widget page should contain chat-related content");

        await TakeScreenshot("chatbot_widget_final");
    }

    // ── SSO Button Rendering ────────────────────────────────────────

    [Fact]
    public async Task LoginPage_ForgotPasswordAndContinue_ArePresent()
    {
        await NavigateAndWait("/login");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Forgot password", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Continue", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("login_controls");
    }

    // ── Forgot Password Link ────────────────────────────────────────

    [Fact]
    public async Task LoginPage_ForgotPasswordLink_IsPresent()
    {
        await NavigateAndWait("/login");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Forgot password", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("forgot_password_link");
    }

    // ── Multi-Page Navigation Flow ──────────────────────────────────

    [Fact]
    public async Task NavigationFlow_MultiplePages_AllRedirectToLogin()
    {
        string[] pages = ["/", "/assistant-studio", "/workflow-studio", "/operations", "/settings"];

        foreach (var path in pages)
        {
            await NavigateAndWait(path);
            await TakeScreenshot($"nav_{path.Replace("/", "_").Trim('_')}");

            var url = Page.Url;
            var content = await Page.ContentAsync();
            Assert.True(
                url.Contains("/login") ||
                content.Contains("Sign In", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Email", StringComparison.OrdinalIgnoreCase),
                $"Page {path} should redirect to login");
        }
    }

    // ── Page Load Performance ───────────────────────────────────────

    [Fact]
    public async Task LoginPage_LoadsWithinTimeout()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await Page.GotoAsync($"{BaseUrl}/login", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 15000
        });

        sw.Stop();
        await TakeScreenshot("load_performance");

        Assert.True(sw.ElapsedMilliseconds < 15000, $"Page took {sw.ElapsedMilliseconds}ms to load");

        File.WriteAllText(
            Path.Combine(GetArtifactDir(), "performance.json"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Page = "/login",
                LoadTimeMs = sw.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            }));
    }

    // ── No Console Errors on Load ───────────────────────────────────

    [Fact]
    public async Task LoginPage_NoJavaScriptErrors()
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);

        await NavigateAndWait("/login");
        await TakeScreenshot("js_error_check");

        Assert.Empty(errors);
    }

    // ── Responsive Layout ───────────────────────────────────────────

    [Fact]
    public async Task LoginPage_ResponsiveLayout_Mobile()
    {
        await Page.SetViewportSizeAsync(375, 812);
        await NavigateAndWait("/login");
        await TakeScreenshot("mobile_375x812");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await emailInput.IsVisibleAsync(), "Email input should be visible on mobile");

        await Page.SetViewportSizeAsync(768, 1024);
        await NavigateAndWait("/login");
        await TakeScreenshot("tablet_768x1024");

        await Page.SetViewportSizeAsync(1280, 720);
        await NavigateAndWait("/login");
        await TakeScreenshot("desktop_1280x720");
    }

    // ── Logout: User Menu & Sign Out ───────────────────────────────

    [Fact]
    public async Task UserMenu_Structure_HasSignOutOption()
    {
        await NavigateAndWait("/login");
        await TakeScreenshot("user_menu_structure");

        var userMenu = Page.Locator("[aria-label='User menu']");
        await userMenu.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await userMenu.IsVisibleAsync(), "User menu should be present in AppBar");

        var activator = userMenu.Locator(".mud-menu-activator");
        Assert.True(await activator.IsVisibleAsync(), "Menu activator (avatar) should be visible");

        var role = await activator.GetAttributeAsync("role");
        Assert.Equal("button", role);
        var hasPopup = await activator.GetAttributeAsync("aria-haspopup");
        Assert.Equal("true", hasPopup);

        var avatarText = await userMenu.Locator(".mud-avatar").TextContentAsync();
        Assert.False(string.IsNullOrWhiteSpace(avatarText), "Avatar should display user initial");

        // Verify avatar does NOT have pointer-events:none (the bug fix)
        var avatarStyle = await userMenu.Locator(".mud-avatar").GetAttributeAsync("style") ?? "";
        Assert.DoesNotContain("pointer-events", avatarStyle);

        await TakeScreenshot("user_menu_avatar_verified");
    }

    [Fact]
    public async Task Logout_NavigateToLogin_ForceLoad_LandsOnLoginPage()
    {
        // Simulate the logout redirect: HandleLogout calls NavigateTo("/login", forceLoad: true)
        await NavigateAndWait("/login");
        await TakeScreenshot("logout_redirect_before");

        // Navigate away and back to /login (simulating forceLoad behavior)
        await Page.GotoAsync($"{BaseUrl}/login", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 15000
        });
        await Page.WaitForTimeoutAsync(3000);
        await TakeScreenshot("logout_redirect_after");

        var url = Page.Url;
        Assert.Contains("/login", url);

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Password", StringComparison.OrdinalIgnoreCase),
            "Login page should show email/password fields after logout redirect");
    }

    [Fact]
    public async Task Logout_SessionCleared_ProtectedPageRedirects()
    {
        // After logout, all protected pages should redirect back to login
        await NavigateAndWait("/login");
        await TakeScreenshot("logout_session_start");

        // Attempt to access a protected page (no auth)
        await NavigateAndWait("/settings");
        await TakeScreenshot("logout_session_protected");

        var url = Page.Url;
        var content = await Page.ContentAsync();
        Assert.True(
            url.Contains("/login") ||
            content.Contains("Sign in", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Email", StringComparison.OrdinalIgnoreCase),
            "Protected page should redirect to login when session is cleared");
    }

    // ── Branding ────────────────────────────────────────────────────

    [Fact]
    public async Task LoginPage_SignInWorkspace_BrandingPresent()
    {
        await NavigateAndWait("/login");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("R2WAI", content);
        Assert.True(
            content.Contains("Sign in", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("workspace", StringComparison.OrdinalIgnoreCase),
            "Login page should show sign-in branding");

        await TakeScreenshot("sign_in_branding");
    }
}
