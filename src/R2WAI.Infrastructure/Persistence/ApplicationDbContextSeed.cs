using Microsoft.EntityFrameworkCore;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Authentication;

namespace R2WAI.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid UserRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid EditorRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid ContributorRoleId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid WorkflowManagerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000005");
    private static readonly Guid UserManagerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000006");
    private static readonly Guid DefaultModelId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Tenants.AnyAsync(cancellationToken))
            return;

        var tenant = new Tenant(DefaultTenantId, "Default", "default");
        tenant.UpdateFeatures(
            """{"chat":true,"documents":true,"knowledge":true,"chatbots":true,"proposals":true,"workflows":true,"assistants":true}""");
        tenant.UpdateSettings(
            """{"maxUsers":100,"maxStorageMb":5000,"maxDocuments":1000}""");

        var adminRole = new Role(AdminRoleId, DefaultTenantId, "Admin", "System administrator with full access", true);
        adminRole.SetPermissions(Permission.All.ToString());

        var userRole = new Role(UserRoleId, DefaultTenantId, "User", "Standard user with basic access", true);
        userRole.SetPermissions(
            (Permission.ConversationRead | Permission.ConversationSend | Permission.DocumentRead |
             Permission.DocumentUpload | Permission.KnowledgeBaseRead | Permission.ChatbotRead).ToString());

        var editorRole = new Role(EditorRoleId, DefaultTenantId, "Editor", "Can manage documents and content", true);
        editorRole.SetPermissions(
            (Permission.DocumentRead | Permission.DocumentUpload | Permission.DocumentDelete |
             Permission.ConversationRead | Permission.ConversationSend | Permission.KnowledgeBaseRead).ToString());

        var contributorRole = new Role(ContributorRoleId, DefaultTenantId, "Contributor", "Can contribute documents", true);
        contributorRole.SetPermissions(
            (Permission.DocumentRead | Permission.DocumentUpload | Permission.ConversationRead |
             Permission.ConversationSend).ToString());

        var workflowManagerRole = new Role(WorkflowManagerRoleId, DefaultTenantId, "WorkflowManager", "Can manage workflows", true);
        workflowManagerRole.SetPermissions(
            (Permission.WorkflowManage | Permission.ConversationRead | Permission.ConversationSend).ToString());

        var userManagerRole = new Role(UserManagerRoleId, DefaultTenantId, "UserManager", "Can manage users", true);
        userManagerRole.SetPermissions(
            (Permission.UserRead | Permission.UserCreate | Permission.UserUpdate | Permission.UserDelete |
             Permission.RoleRead | Permission.ConversationRead).ToString());

        var adminUser = new User(
            AdminUserId, DefaultTenantId,
            "admin@r2wai.io", "admin@r2wai.io",
            "System", "Administrator");
        adminUser.SetPasswordHash(new PasswordHasher().Hash("admin123"));

        var adminUserRole = new UserRole(AdminUserId, AdminRoleId);

        var defaultModel = new ModelConfiguration(
            DefaultModelId, DefaultTenantId,
            "GPT-4o", "OpenAI", "gpt-4o",
            null, null);
        defaultModel.UpdateDetails("GPT-4o", "OpenAI", "gpt-4o", 8192, 0.7, 1.0);
        defaultModel.SetDefault(true);
        defaultModel.Activate();

        context.Tenants.Add(tenant);
        context.Roles.AddRange(adminRole, userRole, editorRole, contributorRole, workflowManagerRole, userManagerRole);
        context.Users.Add(adminUser);
        context.UserRoles.Add(adminUserRole);
        context.ModelConfigurations.Add(defaultModel);

        // --- Demo Assistants ---
        var hrAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000101"),
            DefaultTenantId, "HR Onboarding Assistant", AssistantType.HR,
            DefaultModelId, null);
        hrAssistant.UpdateDetails("HR Onboarding Assistant",
            "Helps new employees navigate onboarding, policies, and benefits.",
            "You are an HR onboarding assistant for our organization. Help new employees understand company policies, benefits enrollment, team introductions, and first-week tasks. Be welcoming, professional, and thorough.",
            null, null);
        hrAssistant.Publish();

        var itAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000102"),
            DefaultTenantId, "IT Helpdesk", AssistantType.IT,
            DefaultModelId, null);
        itAssistant.UpdateDetails("IT Helpdesk",
            "Troubleshoots common IT issues, password resets, and software access.",
            "You are an IT helpdesk assistant. Help employees with password resets, VPN setup, software installation, printer issues, and access requests. Provide step-by-step instructions. Escalate complex issues to the IT team.",
            null, null);
        itAssistant.Publish();

        var financeAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000103"),
            DefaultTenantId, "Finance FAQ", AssistantType.Finance,
            DefaultModelId, null);
        financeAssistant.UpdateDetails("Finance FAQ",
            "Answers questions about expense reports, budgets, and financial policies.",
            "You are a finance assistant. Help employees with expense report submissions, budget inquiries, reimbursement policies, and procurement processes. Reference company financial policies when applicable.",
            null, null);
        financeAssistant.Publish();

        context.AssistantDefinitions.AddRange(hrAssistant, itAssistant, financeAssistant);

        // --- Demo Workflows ---
        var invoiceWorkflow = new Workflow(
            Guid.Parse("00000000-0000-0000-0000-000000000201"),
            DefaultTenantId, AdminUserId, "Invoice Approval",
            "Automated invoice review and multi-level approval process",
            "sequential",
            """[{"name":"Submit Invoice","action":"Action","order":0},{"name":"Manager Review","action":"Approval","assignedRole":"Manager","order":1},{"name":"Finance Approval","action":"Approval","assignedRole":"Finance","order":2},{"name":"Process Payment","action":"Action","order":3},{"name":"Send Confirmation","action":"Email","order":4}]""");
        invoiceWorkflow.Activate();

        var leaveWorkflow = new Workflow(
            Guid.Parse("00000000-0000-0000-0000-000000000202"),
            DefaultTenantId, AdminUserId, "Leave Request",
            "Employee leave request with manager approval",
            "sequential",
            """[{"name":"Submit Request","action":"Action","order":0},{"name":"Manager Approval","action":"Approval","assignedRole":"Manager","order":1},{"name":"HR Notification","action":"Email","order":2},{"name":"Calendar Update","action":"API Call","order":3}]""");
        leaveWorkflow.Activate();

        var onboardingWorkflow = new Workflow(
            Guid.Parse("00000000-0000-0000-0000-000000000203"),
            DefaultTenantId, AdminUserId, "Employee Onboarding",
            "New hire onboarding checklist with IT setup and training",
            "sequential",
            """[{"name":"HR Intake","action":"Action","order":0},{"name":"IT Account Setup","action":"API Call","order":1},{"name":"Manager Introduction","action":"Email","order":2},{"name":"Training Assignment","action":"AI Generate","order":3},{"name":"30-Day Check-in","action":"Approval","assignedRole":"Manager","order":4}]""");

        context.Workflows.AddRange(invoiceWorkflow, leaveWorkflow, onboardingWorkflow);

        // --- Demo Approval Policy ---
        var approvalPolicy = new ApprovalPolicy(
            Guid.Parse("00000000-0000-0000-0000-000000000301"),
            DefaultTenantId, "Default Approval Policy",
            "Standard approval chain for all workflows",
            null, "Admin,WorkflowManager", 1, 60, "Admin");

        context.ApprovalPolicies.Add(approvalPolicy);

        await context.SaveChangesAsync(cancellationToken);
    }
}