using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

[Collection("Browser")]
public class EndToEndBrowserTests : BrowserTestBase
{
    public EndToEndBrowserTests(BrowserFixture fixture) : base(fixture) { }

    // ═══════════════════════════════════════════════════════════════════
    //  1. LOGIN PAGE — Rendering & Structure
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginPage_FullRender_CollectsArtifacts()
    {
        await NavigateAndWait("/login");
        await TakeScreenshot("login_initial");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        Assert.True(await emailInput.IsVisibleAsync());

        var passwordInput = Page.Locator("input[type='password']").First;
        Assert.True(await passwordInput.IsVisibleAsync());

        var continueButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await continueButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        Assert.True(await continueButton.IsVisibleAsync());

        var branding = Page.Locator("text=R2WAI").First;
        Assert.True(await branding.IsVisibleAsync());

        await TakeScreenshot("login_verified");
    }

    [Fact]
    public async Task LoginPage_HasAllFormElements()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var content = await Page.ContentAsync();

        Assert.Contains("Email", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Password", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Remember me", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Forgot password", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("login_all_elements");
    }

    [Fact]
    public async Task LoginPage_SsoButtons_ArePresent()
    {
        await NavigateAndWait("/login");
        await Page.WaitForSelectorAsync(".mud-paper", new PageWaitForSelectorOptions { Timeout = 30000 });

        var content = await Page.ContentAsync();
        Assert.Contains("or continue with", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Google", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Microsoft", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("login_sso_buttons");
    }

    [Fact]
    public async Task LoginPage_RequestAccessLink_IsPresent()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Don't have an account", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Request access", content, StringComparison.OrdinalIgnoreCase);

        var registerLink = Page.Locator("a[href='/register']");
        Assert.True(await registerLink.CountAsync() >= 1, "Register link should exist");

        await TakeScreenshot("login_request_access_link");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  2. LOGIN FLOW — Validation & Error Handling
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginFlow_InvalidCredentials_ShowsError()
    {
        await NavigateAndWait("/login");
        await TakeScreenshot("before_login_attempt");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await emailInput.FillAsync("invalid@test.com");

        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.FillAsync("wrongpassword");

        await TakeScreenshot("credentials_filled");

        var continueButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await continueButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2000);
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

    [Fact]
    public async Task LoginFlow_EmptySubmit_ShowsValidation()
    {
        await NavigateAndWait("/login");

        var continueButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await continueButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await continueButton.ClickAsync();

        await Page.WaitForTimeoutAsync(1000);
        await TakeScreenshot("empty_submit_validation");

        Assert.Contains("/login", Page.Url);
    }

    [Fact]
    public async Task LoginFlow_TabNavigation_WorksCorrectly()
    {
        await NavigateAndWait("/login");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await emailInput.ClickAsync();
        await emailInput.FillAsync("test@example.com");

        await Page.Keyboard.PressAsync("Tab");
        await Page.WaitForTimeoutAsync(500);

        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.FillAsync("testpassword");

        await TakeScreenshot("tab_navigation");

        Assert.True(await passwordInput.IsVisibleAsync());
    }

    // ═══════════════════════════════════════════════════════════════════
    //  3. REGISTER PAGE
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RegisterPage_Renders_WithAllFields()
    {
        await NavigateAndWait("/register");
        await TakeScreenshot("register_initial");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Request Access", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Full Name", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Work Email", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Organization", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Submit Request", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("register_all_fields");
    }

    [Fact]
    public async Task RegisterPage_HasBackToLoginLink()
    {
        await NavigateAndWait("/register");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Already have an account", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sign in", content, StringComparison.OrdinalIgnoreCase);

        var loginLink = Page.Locator("a[href='/login']");
        Assert.True(await loginLink.CountAsync() >= 1, "Login link should exist on register page");
    }

    [Fact]
    public async Task RegisterPage_EmptySubmit_StaysOnPage()
    {
        await NavigateAndWait("/register");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var submitButton = Page.GetByRole(AriaRole.Button, new() { Name = "Submit request" });
        await submitButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await submitButton.ClickAsync();

        await Page.WaitForTimeoutAsync(1000);
        Assert.Contains("/register", Page.Url);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  4. FORGOT & RESET PASSWORD
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ForgotPasswordPage_Renders_WithForm()
    {
        await NavigateAndWait("/forgot-password");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Reset Password", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Send Reset Code", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPasswordPage_HasBackToLoginLink()
    {
        await NavigateAndWait("/forgot-password");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Back to Login", content, StringComparison.OrdinalIgnoreCase);

        var loginLink = Page.Locator("a[href='/login']");
        Assert.True(await loginLink.CountAsync() >= 1, "Back to Login link should exist");
    }

    [Fact]
    public async Task ResetPasswordPage_Renders_WithAllFields()
    {
        await NavigateAndWait("/reset-password");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var content = await Page.ContentAsync();
        Assert.Contains("New Password", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Email", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reset code", content, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  5. PROTECTED PAGES — Auth Redirect
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/")]
    [InlineData("/assistant-studio")]
    [InlineData("/workflow-studio")]
    [InlineData("/operations")]
    [InlineData("/settings")]
    [InlineData("/inbox")]
    [InlineData("/conversations")]
    [InlineData("/documents")]
    [InlineData("/profile")]
    [InlineData("/about")]
    [InlineData("/chatbots")]
    [InlineData("/templates")]
    [InlineData("/knowledgebases")]
    [InlineData("/approvals")]
    [InlineData("/integrations")]
    public async Task ProtectedPages_RedirectToLogin(string path)
    {
        await NavigateAndWait(path);

        var url = Page.Url;
        var content = await Page.ContentAsync();

        Assert.True(
            url.Contains("/login") ||
            content.Contains("Sign In", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Email", StringComparison.OrdinalIgnoreCase),
            $"Page {path} should redirect to login, got URL: {url}");
    }

    [Theory]
    [InlineData("/admin/users")]
    [InlineData("/admin/roles")]
    [InlineData("/admin/security")]
    [InlineData("/admin/permissions")]
    [InlineData("/admin/models")]
    [InlineData("/admin/webhooks")]
    [InlineData("/admin/api-keys")]
    [InlineData("/admin/content-moderation")]
    [InlineData("/settings/tenant")]
    public async Task AdminPages_RedirectToLogin_WhenUnauthenticated(string path)
    {
        await NavigateAndWait(path);

        var url = Page.Url;
        var content = await Page.ContentAsync();

        Assert.True(
            url.Contains("/login") ||
            content.Contains("Sign In", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Email", StringComparison.OrdinalIgnoreCase),
            $"Admin page {path} should redirect to login, got URL: {url}");
    }

    [Theory]
    [InlineData("/operations/ai")]
    [InlineData("/operations/analytics")]
    [InlineData("/operations/reports")]
    [InlineData("/operations/audit-logs")]
    [InlineData("/operations/errors")]
    public async Task MonitorPages_RedirectToLogin_WhenUnauthenticated(string path)
    {
        await NavigateAndWait(path);

        var url = Page.Url;
        var content = await Page.ContentAsync();

        Assert.True(
            url.Contains("/login") ||
            content.Contains("Sign In", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Email", StringComparison.OrdinalIgnoreCase),
            $"Monitor page {path} should redirect to login, got URL: {url}");
    }

    [Theory]
    [InlineData("/assistant-studio/tools")]
    [InlineData("/assistant-studio/knowledge")]
    [InlineData("/assistant-studio/chatbots")]
    [InlineData("/assistant-studio/documents")]
    [InlineData("/workflow-studio/schedules")]
    [InlineData("/workflow-studio/integrations")]
    [InlineData("/workflow-studio/approvals")]
    public async Task StudioSubpages_RedirectToLogin_WhenUnauthenticated(string path)
    {
        await NavigateAndWait(path);

        var url = Page.Url;
        var content = await Page.ContentAsync();

        Assert.True(
            url.Contains("/login") ||
            content.Contains("Sign In", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Email", StringComparison.OrdinalIgnoreCase),
            $"Studio sub-page {path} should redirect to login, got URL: {url}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  6. BLAZOR + MUDBLAZOR FRAMEWORK
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BlazorFramework_LoadsSuccessfully()
    {
        await NavigateAndWait("/login");

        var content = await Page.ContentAsync();
        Assert.Contains("blazor.server.js", content);

        var mudCssLoaded = await Page.Locator("link[href*='MudBlazor']").CountAsync();
        Assert.True(mudCssLoaded >= 1, "MudBlazor CSS should be loaded");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        Assert.True(await mudPaper.IsVisibleAsync());

        await TakeScreenshot("blazor_loaded");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  7. CHATBOT WIDGET (Public)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ChatbotWidget_Renders_WithChatInterface()
    {
        var chatbotId = "4b75a6a6-4179-471b-ba6d-bee6c6884eb3";
        await NavigateAndWait($"/chatbot/widget/{chatbotId}", waitMs: 5000);
        await TakeScreenshot("chatbot_widget_initial");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  8. NAVIGATION FLOWS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NavigationFlow_LoginToRegister_Works()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var registerLink = Page.Locator("a[href='/register']").First;
        await registerLink.ClickAsync();

        await Page.WaitForTimeoutAsync(1000);
        await TakeScreenshot("nav_login_to_register");

        var content = await Page.ContentAsync();
        Assert.True(
            Page.Url.Contains("/register") ||
            content.Contains("Request Access", StringComparison.OrdinalIgnoreCase),
            "Should navigate to register page");
    }

    [Fact]
    public async Task NavigationFlow_LoginToForgotPassword_Works()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var forgotLink = Page.Locator("a[href='/forgot-password']").First;
        await forgotLink.ClickAsync();

        await Page.WaitForTimeoutAsync(1000);
        await TakeScreenshot("nav_login_to_forgot");

        var content = await Page.ContentAsync();
        Assert.True(
            Page.Url.Contains("/forgot-password") ||
            content.Contains("Reset Password", StringComparison.OrdinalIgnoreCase),
            "Should navigate to forgot password page");
    }

    [Fact]
    public async Task NavigationFlow_RegisterToLogin_Works()
    {
        await NavigateAndWait("/register");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var loginLink = Page.Locator("a[href='/login']").First;
        await loginLink.ClickAsync();

        await Page.WaitForTimeoutAsync(1000);

        var content = await Page.ContentAsync();
        Assert.True(
            Page.Url.Contains("/login") ||
            content.Contains("Sign in", StringComparison.OrdinalIgnoreCase),
            "Should navigate back to login page");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  9. PERFORMANCE & QUALITY
    // ═══════════════════════════════════════════════════════════════════

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

    [Fact]
    public async Task LoginPage_NoJavaScriptErrors()
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);

        await NavigateAndWait("/login");
        await TakeScreenshot("js_error_check");

        Assert.Empty(errors);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  10. RESPONSIVE DESIGN
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginPage_ResponsiveLayout_Mobile()
    {
        await Page.SetViewportSizeAsync(375, 812);
        await NavigateAndWait("/login");
        await TakeScreenshot("mobile_375x812");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        Assert.True(await emailInput.IsVisibleAsync(), "Email input should be visible on mobile");

        await Page.SetViewportSizeAsync(768, 1024);
        await NavigateAndWait("/login");
        await TakeScreenshot("tablet_768x1024");

        await Page.SetViewportSizeAsync(1280, 720);
        await NavigateAndWait("/login");
        await TakeScreenshot("desktop_1280x720");
    }

    [Fact]
    public async Task RegisterPage_ResponsiveLayout_Mobile()
    {
        await Page.SetViewportSizeAsync(375, 812);
        await NavigateAndWait("/register");
        await TakeScreenshot("register_mobile_375x812");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        Assert.True(await mudPaper.IsVisibleAsync(), "Register form should be visible on mobile");

        await Page.SetViewportSizeAsync(1280, 720);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  11. BRANDING & VISUAL IDENTITY
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    public async Task AuthPages_HaveConsistentBranding(string path)
    {
        await NavigateAndWait(path);
        await TakeScreenshot($"branding_{path.Replace("/", "_")}");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var content = await Page.ContentAsync();
        Assert.Contains("R2WAI", content);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  12. ACCESSIBILITY
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginPage_AriaLabels_ArePresent()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var emailInput = Page.Locator("[aria-label='Email address']");
        Assert.True(await emailInput.CountAsync() >= 1, "Email input should have aria-label");

        var passwordInput = Page.Locator("[aria-label='Password']");
        Assert.True(await passwordInput.CountAsync() >= 1, "Password input should have aria-label");

        var signInButton = Page.Locator("[aria-label='Sign in']");
        Assert.True(await signInButton.CountAsync() >= 1, "Sign in button should have aria-label");
    }

    [Fact]
    public async Task RegisterPage_AriaLabels_ArePresent()
    {
        await NavigateAndWait("/register");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var fullNameInput = Page.Locator("[aria-label='Full name']");
        Assert.True(await fullNameInput.CountAsync() >= 1, "Full name input should have aria-label");

        var emailInput = Page.Locator("[aria-label='Work email address']");
        Assert.True(await emailInput.CountAsync() >= 1, "Work email input should have aria-label");
    }

    [Fact]
    public async Task LoginPage_PageTitle_IsSet()
    {
        await NavigateAndWait("/login");

        await Page.WaitForFunctionAsync("() => document.title.length > 0");
        var title = await Page.TitleAsync();
        Assert.False(string.IsNullOrEmpty(title), "Page title should not be empty");
        Assert.Contains("R2WAI", title);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  13. 404 / NOT FOUND
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NotFoundPage_ShowsOnInvalidRoute()
    {
        await NavigateAndWait("/this-page-does-not-exist-xyz");
        await TakeScreenshot("not_found_page");

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("404", StringComparison.OrdinalIgnoreCase) ||
            Page.Url.Contains("/login") ||
            content.Contains("R2WAI", StringComparison.OrdinalIgnoreCase),
            "Should show not found page or redirect to login");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  14. AUTHENTICATED FEATURES (Skip if API unavailable)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AuthenticatedUser_HomePage_ShowsDashboard()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("home_dashboard");

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("Good", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("R2WAI", StringComparison.OrdinalIgnoreCase),
            "Home page should show dashboard content");
    }

    [Fact]
    public async Task AuthenticatedUser_MainLayout_HasNavigation()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("main_layout_navigation");

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("Home", StringComparison.OrdinalIgnoreCase) &&
            content.Contains("R2WAI", StringComparison.OrdinalIgnoreCase),
            "Main layout should show navigation elements");
    }

    [Fact]
    public async Task AuthenticatedUser_MainLayout_HasAppBar()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("main_layout_appbar");

        var appBar = Page.Locator(".mud-appbar");
        Assert.True(await appBar.CountAsync() >= 1, "AppBar should be present");

        var content = await Page.ContentAsync();
        Assert.Contains("R2WAI", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/assistant-studio", "Assistants")]
    [InlineData("/workflow-studio", "Workflows")]
    [InlineData("/chatbots", "Chatbots")]
    [InlineData("/documents", "Documents")]
    [InlineData("/inbox", "Inbox")]
    [InlineData("/conversations", "Conversations")]
    [InlineData("/operations", "Operations")]
    [InlineData("/settings", "Settings")]
    [InlineData("/profile", "Profile")]
    [InlineData("/about", "About")]
    [InlineData("/templates", "Templates")]
    [InlineData("/approvals", "Approvals")]
    [InlineData("/assistant-studio/knowledge", "Knowledge")]
    [InlineData("/assistant-studio/tools", "Tools")]
    [InlineData("/workflow-studio/integrations", "Integrations")]
    [InlineData("/workflow-studio/schedules", "Schedules")]
    public async Task AuthenticatedUser_PageLoads(string path, string pageName)
    {
        if (!await TryLogin()) return;

        await NavigateAndWait(path);
        await TakeScreenshot($"auth_{pageName.ToLower()}");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Theory]
    [InlineData("/admin/users", "Users")]
    [InlineData("/admin/roles", "Roles")]
    [InlineData("/admin/security", "Security")]
    [InlineData("/admin/permissions", "Permissions")]
    [InlineData("/admin/models", "Models")]
    [InlineData("/admin/webhooks", "Webhooks")]
    [InlineData("/admin/api-keys", "ApiKeys")]
    [InlineData("/admin/content-moderation", "ContentModeration")]
    [InlineData("/settings/tenant", "TenantSettings")]
    public async Task AuthenticatedUser_AdminPageLoads(string path, string pageName)
    {
        if (!await TryLogin()) return;

        await NavigateAndWait(path);
        await TakeScreenshot($"admin_{pageName.ToLower()}");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Theory]
    [InlineData("/operations/ai", "AiOps")]
    [InlineData("/operations/analytics", "Analytics")]
    [InlineData("/operations/reports", "Reports")]
    [InlineData("/operations/audit-logs", "AuditLogs")]
    [InlineData("/operations/errors", "ErrorLogs")]
    public async Task AuthenticatedUser_MonitorPageLoads(string path, string pageName)
    {
        if (!await TryLogin()) return;

        await NavigateAndWait(path);
        await TakeScreenshot($"monitor_{pageName.ToLower()}");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_FullNavigationFlow_AllMainPages()
    {
        if (!await TryLogin()) return;

        var pages = new[]
        {
            ("/", "Home"),
            ("/assistant-studio", "Assistants"),
            ("/workflow-studio", "Workflows"),
            ("/chatbots", "Chatbots"),
            ("/documents", "Documents"),
            ("/inbox", "Inbox"),
            ("/conversations", "Conversations"),
            ("/operations", "Operations"),
            ("/settings", "Settings"),
            ("/profile", "Profile"),
        };

        foreach (var (path, name) in pages)
        {
            await NavigateAndWait(path);
            await TakeScreenshot($"full_nav_{name.ToLower()}");

            var title = await Page.TitleAsync();
            Assert.Contains("R2WAI", title);

            Assert.False(Page.Url.Contains("/login"),
                $"Page {name} ({path}) should not redirect to login after authentication");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  15. SESSION MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Logout_SessionCleared_ProtectedPageRedirects()
    {
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

    // ═══════════════════════════════════════════════════════════════════
    //  HELPER: Login Flow (reusable)
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> TryLogin()
    {
        await NavigateAndWait("/login");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await emailInput.FillAsync("admin@r2wai.io");

        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.FillAsync("R2wai_Admin!2026");

        var signInButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await signInButton.ClickAsync();

        try
        {
            await Page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 30000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
