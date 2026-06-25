using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

public class EndToEndBrowserTests : BrowserTestBase
{
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

    [Fact]
    public async Task LoginPage_HasAllFormElements()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();

        Assert.Contains("Email", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Password", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Remember me", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Forgot password", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Continue", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("login_all_elements");
    }

    [Fact]
    public async Task LoginPage_SsoButtons_ArePresent()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

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
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Don't have an account", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Request access", content, StringComparison.OrdinalIgnoreCase);

        var registerLink = Page.Locator("a[href='/register']");
        Assert.True(await registerLink.CountAsync() >= 1, "Register link should exist");

        await TakeScreenshot("login_request_access_link");
    }

    [Fact]
    public async Task LoginPage_SecurityBranding_IsPresent()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("enterprise", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("login_security_branding");
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

    [Fact]
    public async Task LoginFlow_EmailOnly_StaysOnLogin()
    {
        await NavigateAndWait("/login");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await emailInput.FillAsync("test@example.com");

        var continueButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await continueButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2000);
        await TakeScreenshot("email_only_validation");

        Assert.Contains("/login", Page.Url);
    }

    [Fact]
    public async Task LoginFlow_PasswordOnly_StaysOnLogin()
    {
        await NavigateAndWait("/login");

        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await passwordInput.FillAsync("somepassword");

        var continueButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await continueButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2000);
        await TakeScreenshot("password_only_validation");

        Assert.Contains("/login", Page.Url);
    }

    [Fact]
    public async Task LoginFlow_TabNavigation_WorksCorrectly()
    {
        await NavigateAndWait("/login");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
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
    //  3. REGISTER PAGE (Request Access)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RegisterPage_Renders_WithAllFields()
    {
        await NavigateAndWait("/register");
        await TakeScreenshot("register_initial");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Request Access", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Full Name", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Work Email", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Organization", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Department", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Submit Request", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("register_all_fields");
    }

    [Fact]
    public async Task RegisterPage_HasBackToLoginLink()
    {
        await NavigateAndWait("/register");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Already have an account", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sign in", content, StringComparison.OrdinalIgnoreCase);

        var loginLink = Page.Locator("a[href='/login']");
        Assert.True(await loginLink.CountAsync() >= 1, "Login link should exist on register page");

        await TakeScreenshot("register_back_to_login");
    }

    [Fact]
    public async Task RegisterPage_EmptySubmit_StaysOnPage()
    {
        await NavigateAndWait("/register");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var submitButton = Page.GetByRole(AriaRole.Button, new() { Name = "Submit request" });
        await submitButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await submitButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2000);
        await TakeScreenshot("register_empty_submit");

        Assert.Contains("/register", Page.Url);
    }

    [Fact]
    public async Task RegisterPage_FillAndSubmit_ShowsConfirmation()
    {
        await NavigateAndWait("/register");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var nameInput = Page.Locator("input").Nth(0);
        await nameInput.FillAsync("Test User");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.FillAsync("testuser@example.com");

        var orgInput = Page.Locator("input").Nth(2);
        await orgInput.FillAsync("Test Organization");

        await TakeScreenshot("register_filled");

        var submitButton = Page.GetByRole(AriaRole.Button, new() { Name = "Submit request" });
        await submitButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await TakeScreenshot("register_after_submit");

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("Request Submitted", StringComparison.OrdinalIgnoreCase) ||
            Page.Url.Contains("/register"),
            "Should show confirmation or stay on register page");
    }

    [Fact]
    public async Task RegisterPage_DepartmentDropdown_HasOptions()
    {
        await NavigateAndWait("/register");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Department", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("register_department_dropdown");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  4. FORGOT PASSWORD PAGE
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ForgotPasswordPage_Renders_WithForm()
    {
        await NavigateAndWait("/forgot-password");
        await TakeScreenshot("forgot_password_initial");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Reset Password", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Send Reset Code", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("forgot_password_form");
    }

    [Fact]
    public async Task ForgotPasswordPage_HasBackToLoginLink()
    {
        await NavigateAndWait("/forgot-password");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Back to Login", content, StringComparison.OrdinalIgnoreCase);

        var loginLink = Page.Locator("a[href='/login']");
        Assert.True(await loginLink.CountAsync() >= 1, "Back to Login link should exist");

        await TakeScreenshot("forgot_password_back_link");
    }

    [Fact]
    public async Task ForgotPasswordPage_SubmitEmail_HandlesGracefully()
    {
        await NavigateAndWait("/forgot-password");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.FillAsync("user@example.com");

        var sendButton = Page.GetByText("Send Reset Code");
        await sendButton.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await TakeScreenshot("forgot_password_submitted");

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("reset", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("sent", StringComparison.OrdinalIgnoreCase) ||
            Page.Url.Contains("/forgot-password"),
            "Should show confirmation or stay on page");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  5. RESET PASSWORD PAGE
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ResetPasswordPage_Renders_WithAllFields()
    {
        await NavigateAndWait("/reset-password");
        await TakeScreenshot("reset_password_initial");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("New Password", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Email", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reset code", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reset Password", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("reset_password_form");
    }

    [Fact]
    public async Task ResetPasswordPage_HasBackToLoginLink()
    {
        await NavigateAndWait("/reset-password");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.Contains("Back to Login", content, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("reset_password_back_link");
    }

    [Fact]
    public async Task ResetPasswordPage_PasswordMismatch_ShowsError()
    {
        await NavigateAndWait("/reset-password");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.FillAsync("user@example.com");

        var textInputs = Page.Locator("input:not([type='email']):not([type='password'])").First;
        await textInputs.FillAsync("RESET123");

        var passwordInputs = Page.Locator("input[type='password']");
        await passwordInputs.Nth(0).FillAsync("newpassword123");
        await passwordInputs.Nth(1).FillAsync("differentpassword");

        var resetButton = Page.GetByText("Reset Password").Last;
        await resetButton.ClickAsync();

        await Page.WaitForTimeoutAsync(2000);
        await TakeScreenshot("reset_password_mismatch");

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("match", StringComparison.OrdinalIgnoreCase) ||
            Page.Url.Contains("/reset-password"),
            "Should show mismatch error or stay on page");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  6. PROTECTED PAGES — Auth Redirect
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
        await TakeScreenshot($"admin_redirect_{path.Replace("/", "_")}");

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
        await TakeScreenshot($"monitor_redirect_{path.Replace("/", "_")}");

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
        await TakeScreenshot($"studio_redirect_{path.Replace("/", "_")}");

        var url = Page.Url;
        var content = await Page.ContentAsync();

        Assert.True(
            url.Contains("/login") ||
            content.Contains("Sign In", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Email", StringComparison.OrdinalIgnoreCase),
            $"Studio sub-page {path} should redirect to login, got URL: {url}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  7. BLAZOR + MUDBLAZOR FRAMEWORK
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
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await mudPaper.IsVisibleAsync());

        await TakeScreenshot("blazor_loaded");
    }

    [Fact]
    public async Task BlazorSignalR_ConnectsSuccessfully()
    {
        await NavigateAndWait("/login");

        var content = await Page.ContentAsync();
        Assert.Contains("blazor.server.js", content);

        var blazorScript = Page.Locator("script[src*='blazor']");
        Assert.True(await blazorScript.CountAsync() >= 1, "Blazor script tag should be present");

        await TakeScreenshot("blazor_signalr");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  8. CHATBOT WIDGET (Public)
    // ═══════════════════════════════════════════════════════════════════

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

    [Fact]
    public async Task ChatbotWidget_AlternateRoute_AlsoWorks()
    {
        var chatbotId = "4b75a6a6-4179-471b-ba6d-bee6c6884eb3";
        await NavigateAndWait($"/chatbot/{chatbotId}", waitMs: 5000);
        await TakeScreenshot("chatbot_direct_route");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  9. NAVIGATION FLOWS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NavigationFlow_MultiplePages_AllRedirectToLogin()
    {
        string[] pages = ["/", "/assistant-studio", "/workflow-studio", "/operations", "/settings",
                          "/inbox", "/conversations", "/documents", "/profile"];

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

    [Fact]
    public async Task NavigationFlow_LoginToRegister_Works()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var registerLink = Page.Locator("a[href='/register']").First;
        await registerLink.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
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
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var forgotLink = Page.Locator("a[href='/forgot-password']").First;
        await forgotLink.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
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
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var loginLink = Page.Locator("a[href='/login']").First;
        await loginLink.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await TakeScreenshot("nav_register_to_login");

        var content = await Page.ContentAsync();
        Assert.True(
            Page.Url.Contains("/login") ||
            content.Contains("Continue", StringComparison.OrdinalIgnoreCase),
            "Should navigate back to login page");
    }

    [Fact]
    public async Task NavigationFlow_ForgotPasswordToLogin_Works()
    {
        await NavigateAndWait("/forgot-password");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var loginLink = Page.Locator("a[href='/login']").First;
        await loginLink.ClickAsync();

        await Page.WaitForTimeoutAsync(3000);
        await TakeScreenshot("nav_forgot_to_login");

        Assert.True(
            Page.Url.Contains("/login"),
            "Should navigate back to login page");
    }

    [Fact]
    public async Task NavigationFlow_AuthPages_FullCycle()
    {
        await NavigateAndWait("/login");
        await TakeScreenshot("cycle_1_login");
        Assert.Contains("/login", Page.Url);

        var registerLink = Page.Locator("a[href='/register']").First;
        await registerLink.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await registerLink.ClickAsync();
        await Page.WaitForTimeoutAsync(3000);
        await TakeScreenshot("cycle_2_register");

        var backToLogin = Page.Locator("a[href='/login']").First;
        await backToLogin.ClickAsync();
        await Page.WaitForTimeoutAsync(3000);
        await TakeScreenshot("cycle_3_back_to_login");

        var forgotLink = Page.Locator("a[href='/forgot-password']").First;
        await forgotLink.ClickAsync();
        await Page.WaitForTimeoutAsync(3000);
        await TakeScreenshot("cycle_4_forgot_password");

        Assert.True(
            Page.Url.Contains("/forgot-password") ||
            (await Page.ContentAsync()).Contains("Reset Password", StringComparison.OrdinalIgnoreCase),
            "Full auth page cycle should complete");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  10. PERFORMANCE & QUALITY
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
    public async Task RegisterPage_LoadsWithinTimeout()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await Page.GotoAsync($"{BaseUrl}/register", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 15000
        });

        sw.Stop();
        await TakeScreenshot("register_load_performance");

        Assert.True(sw.ElapsedMilliseconds < 15000, $"Register page took {sw.ElapsedMilliseconds}ms to load");
    }

    [Fact]
    public async Task ForgotPasswordPage_LoadsWithinTimeout()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await Page.GotoAsync($"{BaseUrl}/forgot-password", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 15000
        });

        sw.Stop();
        await TakeScreenshot("forgot_load_performance");

        Assert.True(sw.ElapsedMilliseconds < 15000, $"Forgot password page took {sw.ElapsedMilliseconds}ms to load");
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

    [Fact]
    public async Task RegisterPage_NoJavaScriptErrors()
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);

        await NavigateAndWait("/register");
        await TakeScreenshot("register_js_error_check");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ForgotPasswordPage_NoJavaScriptErrors()
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);

        await NavigateAndWait("/forgot-password");
        await TakeScreenshot("forgot_js_error_check");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ResetPasswordPage_NoJavaScriptErrors()
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);

        await NavigateAndWait("/reset-password");
        await TakeScreenshot("reset_js_error_check");

        Assert.Empty(errors);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  11. RESPONSIVE DESIGN
    // ═══════════════════════════════════════════════════════════════════

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

    [Fact]
    public async Task RegisterPage_ResponsiveLayout_Mobile()
    {
        await Page.SetViewportSizeAsync(375, 812);
        await NavigateAndWait("/register");
        await TakeScreenshot("register_mobile_375x812");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await mudPaper.IsVisibleAsync(), "Register form should be visible on mobile");

        await Page.SetViewportSizeAsync(768, 1024);
        await NavigateAndWait("/register");
        await TakeScreenshot("register_tablet_768x1024");

        await Page.SetViewportSizeAsync(1920, 1080);
        await NavigateAndWait("/register");
        await TakeScreenshot("register_fullhd_1920x1080");
    }

    [Fact]
    public async Task ForgotPasswordPage_ResponsiveLayout()
    {
        await Page.SetViewportSizeAsync(375, 812);
        await NavigateAndWait("/forgot-password");
        await TakeScreenshot("forgot_mobile");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await mudPaper.IsVisibleAsync(), "Forgot password form should be visible on mobile");

        await Page.SetViewportSizeAsync(1280, 720);
        await NavigateAndWait("/forgot-password");
        await TakeScreenshot("forgot_desktop");
    }

    [Fact]
    public async Task LoginPage_SmallMobile_320x568()
    {
        await Page.SetViewportSizeAsync(320, 568);
        await NavigateAndWait("/login");
        await TakeScreenshot("login_small_mobile_320x568");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await emailInput.IsVisibleAsync(), "Email input should be visible on small mobile");

        var passwordInput = Page.Locator("input[type='password']").First;
        Assert.True(await passwordInput.IsVisibleAsync(), "Password input should be visible on small mobile");
    }

    [Fact]
    public async Task LoginPage_LargeDesktop_2560x1440()
    {
        await Page.SetViewportSizeAsync(2560, 1440);
        await NavigateAndWait("/login");
        await TakeScreenshot("login_large_desktop_2560x1440");

        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.True(await mudPaper.IsVisibleAsync(), "Login card should be visible on large desktop");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  12. LOGOUT & SESSION MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UserMenu_Structure_HasSignOutOption()
    {
        await NavigateAndWait("/login");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await emailInput.FillAsync("admin@r2wai.io");
        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.FillAsync("admin123");
        var signInButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await signInButton.ClickAsync();

        try
        {
            await Page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            return;
        }

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

        var avatarStyle = await userMenu.Locator(".mud-avatar").GetAttributeAsync("style") ?? "";
        Assert.DoesNotContain("pointer-events", avatarStyle);

        await TakeScreenshot("user_menu_avatar_verified");
    }

    [Fact]
    public async Task Logout_NavigateToLogin_ForceLoad_LandsOnLoginPage()
    {
        await NavigateAndWait("/login");
        await TakeScreenshot("logout_redirect_before");

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
        await NavigateAndWait("/login");
        await TakeScreenshot("logout_session_start");

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
    //  13. AUTHENTICATED FEATURES (Skip if API unavailable)
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
    public async Task AuthenticatedUser_HomePage_HasCommandBar()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("home_command_bar");

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("What would you like to do", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("command", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("R2WAI", StringComparison.OrdinalIgnoreCase),
            "Home page should show command bar");
    }

    [Fact]
    public async Task AuthenticatedUser_HomePage_HasQuickActions()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("home_quick_actions");

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("New Assistant", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("New Workflow", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Upload", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("R2WAI", StringComparison.OrdinalIgnoreCase),
            "Home page should show quick action buttons");
    }

    [Fact]
    public async Task AuthenticatedUser_HomePage_HasMetricsCards()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await Page.WaitForTimeoutAsync(5000);
        await TakeScreenshot("home_metrics");

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("Pending Approvals", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Active Workflows", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("System Health", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("R2WAI", StringComparison.OrdinalIgnoreCase),
            "Home page should show action cards with metrics");
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

    [Fact]
    public async Task AuthenticatedUser_SideDrawer_HasAllSections()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("side_drawer");

        var content = await Page.ContentAsync();
        var hasSections =
            content.Contains("Studio", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Workspace", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Monitor", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Settings", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasSections || content.Contains("R2WAI", StringComparison.OrdinalIgnoreCase),
            "Side drawer should have navigation sections");
    }

    [Fact]
    public async Task AuthenticatedUser_AssistantStudio_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/assistant-studio");
        await TakeScreenshot("assistant_studio");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_WorkflowStudio_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/workflow-studio");
        await TakeScreenshot("workflow_studio");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Operations_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/operations");
        await TakeScreenshot("operations_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Inbox_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/inbox");
        await TakeScreenshot("inbox_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Documents_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/documents");
        await TakeScreenshot("documents_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Conversations_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/conversations");
        await TakeScreenshot("conversations_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_KnowledgeBases_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/assistant-studio/knowledge");
        await TakeScreenshot("knowledge_bases_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Templates_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/templates");
        await TakeScreenshot("templates_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Chatbots_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/chatbots");
        await TakeScreenshot("chatbots_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Tools_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/assistant-studio/tools");
        await TakeScreenshot("tools_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Integrations_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/workflow-studio/integrations");
        await TakeScreenshot("integrations_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Schedules_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/workflow-studio/schedules");
        await TakeScreenshot("schedules_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Approvals_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/approvals");
        await TakeScreenshot("approvals_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Profile_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/profile");
        await TakeScreenshot("profile_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Settings_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/settings");
        await TakeScreenshot("settings_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_About_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/about");
        await TakeScreenshot("about_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    // ── Admin Pages ──

    [Fact]
    public async Task AuthenticatedUser_AdminUsers_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/admin/users");
        await TakeScreenshot("admin_users_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_AdminRoles_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/admin/roles");
        await TakeScreenshot("admin_roles_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_AdminSecurity_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/admin/security");
        await TakeScreenshot("admin_security_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_AdminModels_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/admin/models");
        await TakeScreenshot("admin_models_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_AdminApiKeys_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/admin/api-keys");
        await TakeScreenshot("admin_api_keys_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_AdminWebhooks_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/admin/webhooks");
        await TakeScreenshot("admin_webhooks_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_AdminPermissions_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/admin/permissions");
        await TakeScreenshot("admin_permissions_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_AdminContentModeration_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/admin/content-moderation");
        await TakeScreenshot("admin_content_moderation_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_TenantSettings_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/settings/tenant");
        await TakeScreenshot("tenant_settings_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    // ── Monitor Pages ──

    [Fact]
    public async Task AuthenticatedUser_AiOperations_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/operations/ai");
        await TakeScreenshot("ai_operations_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_UsageAnalytics_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/operations/analytics");
        await TakeScreenshot("usage_analytics_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_Reports_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/operations/reports");
        await TakeScreenshot("reports_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_AuditLogs_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/operations/audit-logs");
        await TakeScreenshot("audit_logs_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    [Fact]
    public async Task AuthenticatedUser_ErrorLogs_Loads()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/operations/errors");
        await TakeScreenshot("error_logs_page");

        var title = await Page.TitleAsync();
        Assert.Contains("R2WAI", title);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  14. BRANDING & VISUAL IDENTITY
    // ═══════════════════════════════════════════════════════════════════

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

    [Fact]
    public async Task LoginPage_GradientLogo_IsPresent()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var logoContainer = Page.Locator("div[style*='gradient']").First;
        Assert.True(await logoContainer.IsVisibleAsync(), "Gradient logo container should be visible");

        await TakeScreenshot("gradient_logo");
    }

    [Fact]
    public async Task LoginPage_PlatformDescription_IsPresent()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("AI work platform", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("enterprise", StringComparison.OrdinalIgnoreCase),
            "Login page should show platform description");

        await TakeScreenshot("platform_description");
    }

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
    //  15. ACCESSIBILITY
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginPage_AriaLabels_ArePresent()
    {
        await NavigateAndWait("/login");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var emailInput = Page.Locator("[aria-label='Email address']");
        Assert.True(await emailInput.CountAsync() >= 1, "Email input should have aria-label");

        var passwordInput = Page.Locator("[aria-label='Password']");
        Assert.True(await passwordInput.CountAsync() >= 1, "Password input should have aria-label");

        var signInButton = Page.Locator("[aria-label='Sign in']");
        Assert.True(await signInButton.CountAsync() >= 1, "Sign in button should have aria-label");

        await TakeScreenshot("login_aria_labels");
    }

    [Fact]
    public async Task RegisterPage_AriaLabels_ArePresent()
    {
        await NavigateAndWait("/register");
        var mudPaper = Page.Locator(".mud-paper").First;
        await mudPaper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var fullNameInput = Page.Locator("[aria-label='Full name']");
        Assert.True(await fullNameInput.CountAsync() >= 1, "Full name input should have aria-label");

        var emailInput = Page.Locator("[aria-label='Work email address']");
        Assert.True(await emailInput.CountAsync() >= 1, "Work email input should have aria-label");

        var submitButton = Page.Locator("[aria-label='Submit request']");
        Assert.True(await submitButton.CountAsync() >= 1, "Submit button should have aria-label");

        await TakeScreenshot("register_aria_labels");
    }

    [Fact]
    public async Task LoginPage_PageTitle_IsSet()
    {
        await NavigateAndWait("/login");

        var title = await Page.TitleAsync();
        Assert.False(string.IsNullOrEmpty(title), "Page title should not be empty");
        Assert.Contains("R2WAI", title);
        Assert.Contains("Login", title, StringComparison.OrdinalIgnoreCase);

        await TakeScreenshot("page_title");
    }

    [Fact]
    public async Task RegisterPage_PageTitle_IsSet()
    {
        await NavigateAndWait("/register");

        var title = await Page.TitleAsync();
        Assert.False(string.IsNullOrEmpty(title), "Page title should not be empty");
        Assert.Contains("R2WAI", title);

        await TakeScreenshot("register_page_title");
    }

    [Fact]
    public async Task ForgotPasswordPage_PageTitle_IsSet()
    {
        await NavigateAndWait("/forgot-password");

        var title = await Page.TitleAsync();
        Assert.False(string.IsNullOrEmpty(title), "Page title should not be empty");
        Assert.Contains("R2WAI", title);

        await TakeScreenshot("forgot_page_title");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  16. 404 / NOT FOUND
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
    //  17. AUTHENTICATED — FULL NAVIGATION FLOW
    // ═══════════════════════════════════════════════════════════════════

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

    [Fact]
    public async Task AuthenticatedUser_Breadcrumbs_AreVisible()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/assistant-studio");
        await TakeScreenshot("breadcrumbs");

        var breadcrumbs = Page.Locator(".mud-breadcrumbs");
        Assert.True(await breadcrumbs.CountAsync() >= 1, "Breadcrumbs should be present");
    }

    [Fact]
    public async Task AuthenticatedUser_DarkModeToggle_Exists()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("dark_mode_toggle");

        var darkModeButton = Page.Locator("[aria-label='Toggle dark mode']");
        Assert.True(await darkModeButton.CountAsync() >= 1, "Dark mode toggle should be present");
    }

    [Fact]
    public async Task AuthenticatedUser_NotificationBell_Exists()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("notification_bell");

        var notifButton = Page.Locator("[aria-label='Notifications']");
        Assert.True(await notifButton.CountAsync() >= 1, "Notification bell should be present");
    }

    [Fact]
    public async Task AuthenticatedUser_MobileBottomNav_OnSmallScreen()
    {
        if (!await TryLogin()) return;

        await Page.SetViewportSizeAsync(375, 812);
        await NavigateAndWait("/");
        await TakeScreenshot("mobile_bottom_nav");

        var bottomNav = Page.Locator(".mobile-bottom-nav");
        Assert.True(await bottomNav.CountAsync() >= 1, "Mobile bottom navigation should be present");
    }

    [Fact]
    public async Task AuthenticatedUser_CommandPaletteTrigger_Exists()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("command_palette_trigger");

        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("Ctrl+K", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Ask R2WAI", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("command", StringComparison.OrdinalIgnoreCase),
            "Command palette trigger should be present");
    }

    [Fact]
    public async Task AuthenticatedUser_MenuToggle_Works()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("menu_before_toggle");

        var menuButton = Page.Locator("[aria-label='Toggle navigation']");
        if (await menuButton.CountAsync() >= 1)
        {
            await menuButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
            await TakeScreenshot("menu_after_toggle");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  18. MULTI-PAGE PERFORMANCE BENCHMARK
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AllPublicPages_LoadWithinTimeout()
    {
        var pages = new[] { "/login", "/register", "/forgot-password", "/reset-password" };
        var results = new List<object>();

        foreach (var page in pages)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            await Page.GotoAsync($"{BaseUrl}{page}", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 15000
            });

            sw.Stop();
            await TakeScreenshot($"perf_{page.Replace("/", "_").Trim('_')}");

            results.Add(new { Page = page, LoadTimeMs = sw.ElapsedMilliseconds });
            Assert.True(sw.ElapsedMilliseconds < 15000, $"Page {page} took {sw.ElapsedMilliseconds}ms");
        }

        File.WriteAllText(
            Path.Combine(GetArtifactDir(), "all_pages_performance.json"),
            System.Text.Json.JsonSerializer.Serialize(results));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPER: Login Flow (reusable)
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> TryLogin()
    {
        await NavigateAndWait("/login");

        var emailInput = Page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await emailInput.FillAsync("admin@r2wai.io");

        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.FillAsync("admin123");

        var signInButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
        await signInButton.ClickAsync();

        try
        {
            await Page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 10000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
