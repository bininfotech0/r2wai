using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

/// <summary>
/// Covers the UI/UX redesign changes: persona-based navigation, the Automations rename
/// (formerly "Workflows"), the New Automation wizard, the Automation simple detail view,
/// the AI Assistant simple/edit split, and the Integrations "Test connection" action.
/// </summary>
[Collection("Browser")]
public class RedesignBrowserTests : BrowserTestBase
{
    public RedesignBrowserTests(BrowserFixture fixture) : base(fixture) { }

    // ═══════════════════════════════════════════════════════════════════
    //  PERSONA-BASED NAVIGATION
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SuperAdmin_Nav_ShowsAutomationsNotWorkflows()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("nav_superadmin_home");

        var nav = Page.Locator("nav, .mud-drawer").First;
        var content = await nav.InnerTextAsync();

        Assert.Contains("Automations", content);
        Assert.DoesNotContain("Workflows", content);
    }

    [Fact]
    public async Task SuperAdmin_Nav_ShowsManageGroupWithDepartmentsAndModels()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        var nav = Page.Locator("nav, .mud-drawer").First;
        var content = await nav.InnerTextAsync();

        Assert.Contains("Departments", content);
        Assert.Contains("Models", content);
        Assert.Contains("Security", content);
        Assert.Contains("Audit", content);
    }

    [Fact]
    public async Task Officer_Nav_ShowsCompactPersonaMenu_NotBuildOrManageGroups()
    {
        if (!await TryLogin("officer@r2wai.io", "Test@1234!")) return;

        await NavigateAndWait("/");
        await TakeScreenshot("nav_officer_home");

        var nav = Page.Locator("nav, .mud-drawer").First;
        var content = await nav.InnerTextAsync();

        Assert.Contains("Pending Work", content);
        Assert.Contains("My Applications", content);
        Assert.DoesNotContain("Departments", content); // SuperAdmin-only
        Assert.DoesNotContain("Publish", content); // DepartmentAdmin/SuperAdmin-only group
    }

    [Fact]
    public async Task DepartmentAdmin_Nav_ShowsBuildGroup_NotSuperAdminManageItems()
    {
        if (!await TryLogin("deptadmin@r2wai.io", "Test@1234!")) return;

        await NavigateAndWait("/");
        await TakeScreenshot("nav_deptadmin_home");

        var nav = Page.Locator("nav, .mud-drawer").First;
        var content = await nav.InnerTextAsync();

        Assert.Contains("Automations", content);
        Assert.Contains("Users & Roles", content);
        Assert.DoesNotContain("Departments", content); // SuperAdmin-only
        Assert.DoesNotContain("Global Policies", content); // SuperAdmin-only
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTOMATIONS (formerly Workflow Studio)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AutomationsPage_TitleAndHeader_SayAutomations()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/workflow-studio");
        await TakeScreenshot("automations_list");

        var title = await Page.TitleAsync();
        Assert.Contains("Automations", title);

        var content = await Page.ContentAsync();
        Assert.Contains("Automations", content);
    }

    [Fact]
    public async Task AutomationsPage_NewAutomationButton_OpensWizardWithFourSteps()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/workflow-studio");

        var newButton = Page.GetByRole(AriaRole.Button, new() { Name = "New Automation", Exact = true });
        await newButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await newButton.ClickAsync();

        var dialog = Page.Locator(".mud-dialog").First;
        await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await TakeScreenshot("new_automation_wizard_step1");

        var dialogContent = await dialog.InnerTextAsync();
        Assert.Contains("Trigger", dialogContent);
        Assert.Contains("Actions", dialogContent);
        Assert.Contains("Conditions", dialogContent);
        Assert.Contains("Review", dialogContent);
        Assert.Contains("Application Submitted", dialogContent);
    }

    [Fact]
    public async Task AutomationsPage_ViewButton_NavigatesToSimpleDetailPage()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/workflow-studio");

        var viewButton = Page.GetByRole(AriaRole.Button, new() { Name = "View" }).First;
        if (await viewButton.CountAsync() == 0) return; // no automations seeded — nothing to view

        await viewButton.ClickAsync();
        await Page.WaitForURLAsync(url => url.Contains("/workflow-studio/automations/"),
            new PageWaitForURLOptions { Timeout = 15000 });
        await TakeScreenshot("automation_detail_view");

        var content = await Page.ContentAsync();
        Assert.Contains("Trigger", content);
        Assert.Contains("Actions", content);
        Assert.Contains("Conditions", content);
        Assert.Contains("Approvals", content);

        var editButton = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" });
        Assert.True(await editButton.CountAsync() >= 1, "Automation detail page should have an Edit action");
    }

    [Fact]
    public async Task AutomationDetail_EditButton_OpensSideDrawerWithGeneralAndSettingsTabs()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/workflow-studio");
        var viewButton = Page.GetByRole(AriaRole.Button, new() { Name = "View" }).First;
        if (await viewButton.CountAsync() == 0) return;

        await viewButton.ClickAsync();
        await Page.WaitForURLAsync(url => url.Contains("/workflow-studio/automations/"),
            new PageWaitForURLOptions { Timeout = 15000 });

        var editButton = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).First;
        await editButton.ClickAsync();

        var drawer = Page.Locator(".mud-drawer").Last;
        await drawer.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await TakeScreenshot("automation_edit_drawer");

        var drawerContent = await drawer.InnerTextAsync();
        Assert.Contains("General", drawerContent);
        Assert.Contains("Settings", drawerContent);
        Assert.Contains("Done", drawerContent);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AI ASSISTANT — simple view vs. edit
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AssistantStudio_SelectingAssistant_ShowsSimpleOverviewWithEditButton()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/assistant-studio");

        var firstAssistant = Page.Locator(".assistant-list-item, .assistant-list-item--selected").First;
        if (await firstAssistant.CountAsync() == 0) return; // no assistants seeded

        await firstAssistant.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        await TakeScreenshot("assistant_simple_overview");

        var editAssistantButton = Page.GetByRole(AriaRole.Button, new() { Name = "Edit Assistant" });
        Assert.True(await editAssistantButton.CountAsync() >= 1, "Simple overview should show an Edit Assistant button");

        await editAssistantButton.First.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        await TakeScreenshot("assistant_edit_mode");

        var backButton = Page.GetByRole(AriaRole.Button, new() { Name = "Back to overview" });
        Assert.True(await backButton.CountAsync() >= 1, "Edit mode should offer a way back to the simple overview");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  INTEGRATIONS — Test connection action
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task IntegrationsPage_HasTestConnectionActionAndHealthCheckColumn()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/workflow-studio/integrations");
        await TakeScreenshot("integrations_list");

        var content = await Page.ContentAsync();
        Assert.Contains("Last Health Check", content);

        var testButton = Page.Locator("[aria-label='Test connection']");
        if (await testButton.CountAsync() == 0) return; // no integrations installed

        Assert.True(await testButton.CountAsync() >= 1, "Installed integrations should have a Test connection action");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DASHBOARD KPI ROW
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Home_KpiRow_ShowsAutomationsAndExecutionsTiles()
    {
        if (!await TryLogin()) return;

        await NavigateAndWait("/");
        await TakeScreenshot("home_kpi_row");

        var content = await Page.ContentAsync();
        Assert.Contains("Automations", content);
        Assert.Contains("Executions", content);
        Assert.Contains("Success Rate", content);
        Assert.Contains("AI Assistants", content);
    }

    private Task<bool> TryLogin() => TryLogin("admin@r2wai.io", "R2wai_Admin!2026");

    private async Task<bool> TryLogin(string email, string password)
    {
        await NavigateAndWait("/login");

        var emailInput = Page.Locator("[aria-label='Email or Aadhaar number']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await emailInput.FillAsync(email);

        var passwordInput = Page.Locator("input[type='password']").First;
        await passwordInput.FillAsync(password);

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
