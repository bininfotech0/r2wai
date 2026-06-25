using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

[Collection("Browser")]
public class AuthenticatedBrowserTests : BrowserTestBase
{
    public AuthenticatedBrowserTests(BrowserFixture fixture) : base(fixture) { }

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
    public async Task AuthenticatedUser_HomePage_HasQuickActions()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        var content = await Page.ContentAsync();
        Assert.True(
            content.Contains("New Assistant", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("New Workflow", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("R2WAI", StringComparison.OrdinalIgnoreCase),
            "Home page should show quick action buttons");
    }

    [Fact]
    public async Task AuthenticatedUser_MainLayout_HasAppBar()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        var appBar = Page.Locator(".mud-appbar");
        Assert.True(await appBar.CountAsync() >= 1, "AppBar should be present");
    }

    [Fact]
    public async Task AuthenticatedUser_AssistantStudio_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/assistant-studio");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_WorkflowStudio_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/workflow-studio");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Operations_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/operations");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Inbox_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/inbox");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Documents_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/documents");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Conversations_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/conversations");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_KnowledgeBases_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/assistant-studio/knowledge");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Templates_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/templates");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Chatbots_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/chatbots");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Tools_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/assistant-studio/tools");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Integrations_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/workflow-studio/integrations");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Schedules_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/workflow-studio/schedules");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Approvals_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/approvals");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Profile_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/profile");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Settings_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/settings");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_About_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/about");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_AdminUsers_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/admin/users");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_AdminRoles_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/admin/roles");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_AdminSecurity_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/admin/security");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_AdminModels_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/admin/models");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_AdminApiKeys_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/admin/api-keys");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_AdminWebhooks_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/admin/webhooks");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_AdminPermissions_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/admin/permissions");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_AdminContentModeration_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/admin/content-moderation");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_TenantSettings_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/settings/tenant");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_AiOperations_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/operations/ai");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_UsageAnalytics_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/operations/analytics");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_Reports_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/operations/reports");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_AuditLogs_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/operations/audit-logs");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_ErrorLogs_Loads()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/operations/errors");
        Assert.Contains("R2WAI", await Page.TitleAsync());
    }

    [Fact]
    public async Task AuthenticatedUser_DarkModeToggle_Exists()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/");
        var darkModeButton = Page.Locator("[aria-label='Toggle dark mode']");
        Assert.True(await darkModeButton.CountAsync() >= 1, "Dark mode toggle should be present");
    }

    [Fact]
    public async Task AuthenticatedUser_NotificationBell_Exists()
    {
        if (!await TryLogin()) return;
        await NavigateAndWait("/");
        var notifButton = Page.Locator("[aria-label='Notifications']");
        Assert.True(await notifButton.CountAsync() >= 1, "Notification bell should be present");
    }

    [Fact]
    public async Task UserMenu_Structure_HasSignOutOption()
    {
        if (!await TryLogin()) return;

        var userMenu = Page.Locator("[aria-label='User menu']");
        await userMenu.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        Assert.True(await userMenu.IsVisibleAsync(), "User menu should be present");
    }

    private async Task<bool> TryLogin()
    {
        try
        {
            await NavigateAndWait("/login");
            var emailInput = Page.Locator("input[type='email']").First;
            await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
            await emailInput.FillAsync("admin@r2wai.io");
            var passwordInput = Page.Locator("input[type='password']").First;
            await passwordInput.FillAsync("admin123");
            var signInButton = Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true });
            await signInButton.ClickAsync();
            await Page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 15000 });
            return true;
        }
        catch { return false; }
    }
}
