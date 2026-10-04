using Microsoft.EntityFrameworkCore;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Authentication;

namespace R2WAI.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid StandardUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid DepartmentAdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid UserRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid SystemAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000007");
    private static readonly Guid DefaultModelId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherTenantAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid OtherTenantAdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid CrossTenantProbeAssistantId = Guid.Parse("00000000-0000-0000-0000-000000000501");

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

        // Referenced throughout the API as [Authorize(Roles="Admin,SystemAdmin")] (AdminController,
        // ApiKeysController, DepartmentsController, GovernanceController, MembersController,
        // OperationsController, WebhooksController) and required for the frontend's SuperAdmin nav
        // persona (Tools & APIs / AI Models / Security & Policies — see roleNav.ts) — but never
        // actually seeded anywhere, so no user could ever hold it. Confirmed live: with only the
        // Admin role, admin@r2wai.io's nav never showed those three pages at all, even though their
        // backend routes work fine. Seeded here and granted to the bootstrap admin alongside Admin
        // (additive — every existing Admin-only behavior is unaffected) so the platform actually has
        // one reachable top-tier account, matching what a fresh deployment needs on day one.
        var systemAdminRole = new Role(SystemAdminRoleId, DefaultTenantId, "SystemAdmin", "Platform-wide super administrator", true);
        systemAdminRole.SetPermissions(Permission.All.ToString());

        var userRole = new Role(UserRoleId, DefaultTenantId, "User", "Standard user with basic access", true);
        userRole.SetPermissions(
            (Permission.ConversationRead | Permission.ConversationSend | Permission.DocumentRead |
             Permission.DocumentUpload | Permission.KnowledgeBaseRead | Permission.ChatbotRead).ToString());

        // Editor/Contributor/WorkflowManager/UserManager retired 2026-08-29 (CollapseRbacToThreeRoles
        // migration): the nav (roleNav.ts) already mapped all four straight to the "Admin" persona,
        // so the extra granularity was UI-invisible and security-relevant-only-in-theory. The real
        // RBAC model is now exactly the 3 roles the nav implies — Admin/User/SystemAdmin.

        var adminUser = new User(
            AdminUserId, DefaultTenantId,
            "admin@r2wai.io", "admin@r2wai.io",
            "System", "Administrator");
        adminUser.SetPasswordHash(new PasswordHasher().Hash("R2wai_Admin!2026"));

        var adminUserRole = new UserRole(AdminUserId, AdminRoleId);
        var adminUserSystemAdminRole = new UserRole(AdminUserId, SystemAdminRoleId);

        // A plain-"User"-role account exists purely so role-matrix tests have something to assert
        // *against* — every seeded account before this was Admin, so no test could ever tell the
        // difference between "authorization is enforced" and "the only account happens to be an
        // admin". See tests/R2WAI.Api.Tests/Security/RoleMatrixSecurityTests.cs.
        var standardUser = new User(
            StandardUserId, DefaultTenantId,
            "user@r2wai.io", "user@r2wai.io",
            "Standard", "User");
        standardUser.SetPasswordHash(new PasswordHasher().Hash("R2wai_User!2026"));
        var standardUserRole = new UserRole(StandardUserId, UserRoleId);

        // A plain-"Admin"-role account WITHOUT SystemAdmin — every prior seeded "admin" account
        // held both roles together, so no test could ever tell "Super Admin-only routes are
        // actually enforced" apart from "the only Admin account happens to also be a SystemAdmin".
        // Confirmed live this session: GovernanceController/CapabilitiesController writes/
        // AdminController's /models routes previously accepted plain Admin too, even though
        // roleNav.ts hides Security & Policies / Tools & APIs / AI Models from the Admin persona
        // entirely — exactly the "UI permission treated as security" failure the checklist warns
        // about. See RoleMatrixSecurityTests.
        var departmentAdminUser = new User(
            DepartmentAdminUserId, DefaultTenantId,
            "deptadmin@r2wai.io", "deptadmin@r2wai.io",
            "Department", "Admin");
        departmentAdminUser.SetPasswordHash(new PasswordHasher().Hash("R2wai_DeptAdmin!2026"));
        var departmentAdminUserRole = new UserRole(DepartmentAdminUserId, AdminRoleId);

        var defaultModel = new ModelConfiguration(
            DefaultModelId, DefaultTenantId,
            "GPT-4o", "OpenAI", "gpt-4o",
            null, null);
        defaultModel.UpdateDetails("GPT-4o", "OpenAI", "gpt-4o", 8192, 0.7, 1.0);
        defaultModel.SetDefault(true);
        defaultModel.Activate();

        context.Tenants.Add(tenant);
        context.Roles.AddRange(adminRole, userRole, systemAdminRole);
        context.Users.AddRange(adminUser, standardUser, departmentAdminUser);
        context.UserRoles.AddRange(adminUserRole, adminUserSystemAdminRole, standardUserRole, departmentAdminUserRole);
        context.ModelConfigurations.Add(defaultModel);

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

        var demoBookingWorkflow = new Workflow(
            Guid.Parse("00000000-0000-0000-0000-000000000204"),
            DefaultTenantId, AdminUserId, "Demo Class Booking",
            "Books a free demo class for a prospective student and reminds them before the slot",
            "sequential",
            """[{"name":"Inquiry Received","action":"Action","order":0},{"name":"Capture Student Details","action":"AI Generate","order":1},{"name":"Check Slot Availability","action":"Database","order":2},{"name":"Confirm Booking","action":"Email","order":3},{"name":"Send Reminder","action":"Email","order":4}]""");
        demoBookingWorkflow.Activate();

        var feeReminderWorkflow = new Workflow(
            Guid.Parse("00000000-0000-0000-0000-000000000205"),
            DefaultTenantId, AdminUserId, "Fee Reminder Automation",
            "Chases students with overdue monthly fees via WhatsApp and email",
            "sequential",
            """[{"name":"Detect Due Fee","action":"Database","order":0},{"name":"Send Payment Reminder","action":"Email","order":1},{"name":"Send Payment Link","action":"API Call","order":2},{"name":"Escalate If Unpaid","action":"Approval","assignedRole":"Admin","order":3}]""");
        feeReminderWorkflow.Activate();

        var resultBroadcastWorkflow = new Workflow(
            Guid.Parse("00000000-0000-0000-0000-000000000206"),
            DefaultTenantId, AdminUserId, "Exam Result Broadcast",
            "Notifies enrolled students of JSSC/JPSC exam dates, admit cards, and results",
            "sequential",
            """[{"name":"Fetch Exam Update","action":"Action","order":0},{"name":"Draft Announcement","action":"AI Generate","order":1},{"name":"Broadcast To Students","action":"Email","order":2},{"name":"Log Delivery","action":"Action","order":3}]""");
        resultBroadcastWorkflow.Activate();

        context.Workflows.AddRange(
            invoiceWorkflow, leaveWorkflow, onboardingWorkflow,
            demoBookingWorkflow, feeReminderWorkflow, resultBroadcastWorkflow);

        // --- Demo Departments & Connected Applications ---
        var digitalServicesDept = new Department(
            Guid.Parse("00000000-0000-0000-0000-000000000601"),
            DefaultTenantId, "Digital Services", "DIG",
            "Manages citizen-facing digital services and connected legacy systems.");

        var revenueDept = new Department(
            Guid.Parse("00000000-0000-0000-0000-000000000602"),
            DefaultTenantId, "Revenue", "REV",
            "Property tax, permits, and collections.");

        context.Departments.AddRange(digitalServicesDept, revenueDept);

        var taxPortal = new ConnectedApplication(
            Guid.Parse("00000000-0000-0000-0000-000000000701"),
            DefaultTenantId, revenueDept.Id, "Property Tax Portal", "PROP-TAX",
            "Online property tax assessment and payment portal.",
            "https://tax.r2wai.example");
        taxPortal.UpdateDetails("Property Tax Portal", "Online property tax assessment and payment portal.",
            "https://tax.r2wai.example", ApplicationEnvironment.Development);
        taxPortal.Publish();

        var permitSystem = new ConnectedApplication(
            Guid.Parse("00000000-0000-0000-0000-000000000702"),
            DefaultTenantId, digitalServicesDept.Id, "Permit Management System", "PERMIT",
            "Construction and business permit application tracking.",
            "https://permits.r2wai.example");
        permitSystem.UpdateDetails("Permit Management System", "Construction and business permit application tracking.",
            "https://permits.r2wai.example", ApplicationEnvironment.Staging);
        permitSystem.MarkConfiguring();

        context.Applications.AddRange(taxPortal, permitSystem);

        // --- R2WAI Studio: initial Application + AI Assistant set ---
        // Five fresh Draft assistants, one per application, replacing the removed demo assistants
        // above. Capability lists stay empty (KnowledgeBaseId/Tools null) until real application/API
        // requirements are configured per ADR — see BusinessCapabilities cleanup task. "Niryaat" is the
        // one accepted spelling; do not introduce Nriyaat/Niryat elsewhere.
        var employeePortal = new ConnectedApplication(
            Guid.Parse("00000000-0000-0000-0000-000000000801"),
            DefaultTenantId, digitalServicesDept.Id, "Employee Portal", "EMP-PORTAL",
            "Employee self-service portal.");

        var supplierPortal = new ConnectedApplication(
            Guid.Parse("00000000-0000-0000-0000-000000000802"),
            DefaultTenantId, digitalServicesDept.Id, "Supplier Portal", "SUP-PORTAL",
            "Supplier-facing services and business processes.");

        var samparkPortal = new ConnectedApplication(
            Guid.Parse("00000000-0000-0000-0000-000000000803"),
            DefaultTenantId, digitalServicesDept.Id, "Sampark Portal", "SAMPARK-PORTAL",
            "Sampark Portal services.");

        var niryaatPortal = new ConnectedApplication(
            Guid.Parse("00000000-0000-0000-0000-000000000804"),
            DefaultTenantId, digitalServicesDept.Id, "Niryaat Portal", "NIRYAAT-PORTAL",
            "Niryaat Portal services.");

        var enterpriseApp = new ConnectedApplication(
            Guid.Parse("00000000-0000-0000-0000-000000000805"),
            DefaultTenantId, digitalServicesDept.Id, "Enterprise / Cross Application", "ENTERPRISE",
            "Future cross-application enterprise scope.");

        context.Applications.AddRange(employeePortal, supplierPortal, samparkPortal, niryaatPortal, enterpriseApp);

        var employeeAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000901"),
            DefaultTenantId, "Employee Assistant", AssistantType.General, DefaultModelId, null);
        employeeAssistant.UpdateDetails("Employee Assistant",
            "Employee self-service and intelligent access to Employee Portal capabilities.",
            "You are the Employee Assistant for the Employee Portal. Help employees with self-service tasks and questions. Only reference capabilities that have been explicitly configured for you — do not claim to perform actions you cannot verify.",
            null, null);
        employeeAssistant.AssignApplication(employeePortal.Id);

        var supplierAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000902"),
            DefaultTenantId, "Supplier Assistant", AssistantType.General, DefaultModelId, null);
        supplierAssistant.UpdateDetails("Supplier Assistant",
            "Supplier-facing intelligent access to supplier information, services and business processes.",
            "You are the Supplier Assistant for the Supplier Portal. Help suppliers with information, services, and business processes. Only reference capabilities that have been explicitly configured for you — do not claim to perform actions you cannot verify.",
            null, null);
        supplierAssistant.AssignApplication(supplierPortal.Id);

        var samparkAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000903"),
            DefaultTenantId, "Sampark Assistant", AssistantType.General, DefaultModelId, null);
        samparkAssistant.UpdateDetails("Sampark Assistant",
            "Intelligent access to Sampark Portal services.",
            "You are the Sampark Assistant for the Sampark Portal. Only reference capabilities that have been explicitly configured for you — do not claim to perform actions you cannot verify.",
            null, null);
        samparkAssistant.AssignApplication(samparkPortal.Id);

        var niryaatAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000904"),
            DefaultTenantId, "Niryaat Assistant", AssistantType.General, DefaultModelId, null);
        niryaatAssistant.UpdateDetails("Niryaat Assistant",
            "Intelligent access to Niryaat Portal services.",
            "You are the Niryaat Assistant for the Niryaat Portal. Only reference capabilities that have been explicitly configured for you — do not claim to perform actions you cannot verify.",
            null, null);
        niryaatAssistant.AssignApplication(niryaatPortal.Id);

        var enterpriseAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000905"),
            DefaultTenantId, "Enterprise Assistant", AssistantType.General, DefaultModelId, null);
        enterpriseAssistant.UpdateDetails("Enterprise Assistant",
            "Future cross-application enterprise assistant.",
            "You are the Enterprise Assistant, scoped for future cross-application enterprise use. Only reference capabilities that have been explicitly configured for you — do not claim to perform actions you cannot verify.",
            null, null);
        enterpriseAssistant.AssignApplication(enterpriseApp.Id);

        context.AssistantDefinitions.AddRange(
            employeeAssistant, supplierAssistant, samparkAssistant, niryaatAssistant, enterpriseAssistant);

        // --- Demo Approval Policy ---
        var approvalPolicy = new ApprovalPolicy(
            Guid.Parse("00000000-0000-0000-0000-000000000301"),
            DefaultTenantId, "Default Approval Policy",
            "Standard approval chain for all workflows",
            null, """["Admin"]""", 1, 60, """["Admin"]""");

        context.ApprovalPolicies.Add(approvalPolicy);

        // --- Built-in AI Tool Governance ---
        // One platform-level ToolDefinition ("Capability") row per [KernelFunction] exposed by the
        // Semantic Kernel plugins (src/R2WAI.Infrastructure/AI/Plugins/), so AiFunctionAuditFilter has
        // something to enforce and admins can tune it via the existing Capabilities UI. ApplicationId
        // stays null (platform-wide, not tied to one connected application). Defaults preserve today's
        // behavior exactly — RequiredRole unset, ApprovalRequired false — so seeding this doesn't newly
        // block anything; an admin opts into stricter enforcement per function afterward.
        var builtInTools = new (string Id, string Name, string Description, string RiskLevel)[]
        {
            ("00000000-0000-0000-0000-000000000401", "start_workflow", "Start (run) a workflow by name or ID", "Medium"),
            ("00000000-0000-0000-0000-000000000402", "submit_approval_request", "Submit a new approval request for a workflow instance", "Medium"),
            ("00000000-0000-0000-0000-000000000403", "get_workflow_status", "Get the current status of a workflow instance", "Low"),
            ("00000000-0000-0000-0000-000000000404", "notify_approver", "Send a notification to a workflow approver", "Medium"),
            ("00000000-0000-0000-0000-000000000405", "list_pending_approvals", "List pending approval requests for the current user", "Low"),
            ("00000000-0000-0000-0000-000000000406", "search_knowledge_base", "Search a knowledge base using semantic search", "Low"),
            ("00000000-0000-0000-0000-000000000407", "retrieve_documents", "Retrieve documents from a knowledge base", "Low"),
            ("00000000-0000-0000-0000-000000000408", "get_citations", "Get citations from search results", "Low"),
            ("00000000-0000-0000-0000-000000000409", "summarize_document", "Summarize a document by its ID", "Low"),
            ("00000000-0000-0000-0000-000000000410", "extract_from_document", "Extract structured data from a document using a schema", "Low"),
            ("00000000-0000-0000-0000-000000000411", "compare_documents", "Compare two documents and return the comparison result", "Low"),
            ("00000000-0000-0000-0000-000000000412", "ask_document", "Ask a question about a document", "Low"),
            ("00000000-0000-0000-0000-000000000413", "get_assistant_context", "Get the context and configuration for an assistant", "Low"),
            ("00000000-0000-0000-0000-000000000414", "get_knowledge_base_context", "Get context from a knowledge base for answering questions", "Low"),
        };

        foreach (var (id, name, description, riskLevel) in builtInTools)
        {
            var tool = new ToolDefinition(Guid.Parse(id), DefaultTenantId, name, ToolType.SemanticKernelFunction, description);
            tool.ConfigureGovernance(riskLevel, requiredRole: null, confirmationRequired: false, approvalRequired: false, auditRequired: true);
            context.ToolDefinitions.Add(tool);
        }

        // --- Second tenant, exists purely so cross-tenant isolation can actually be regression-tested.
        // Every account seeded above this line belongs to the one DefaultTenantId — no prior test could
        // tell "tenant isolation is enforced" apart from "there's only ever been one tenant to query
        // against". Mirrors the same reasoning as the standardUser/departmentAdminUser accounts above.
        var otherTenant = new Tenant(OtherTenantId, "Other Tenant", "other-tenant");
        var otherTenantAdminRole = new Role(OtherTenantAdminRoleId, OtherTenantId, "Admin", "System administrator with full access", true);
        otherTenantAdminRole.SetPermissions(Permission.All.ToString());
        var otherTenantAdminUser = new User(
            OtherTenantAdminUserId, OtherTenantId,
            "othertenant-admin@r2wai.io", "othertenant-admin@r2wai.io",
            "Other", "TenantAdmin");
        otherTenantAdminUser.SetPasswordHash(new PasswordHasher().Hash("R2wai_OtherTenant!2026"));
        var otherTenantAdminUserRole = new UserRole(OtherTenantAdminUserId, OtherTenantAdminRoleId);
        // A real resource under DefaultTenantId for cross-tenant tests to attempt reaching — the EF
        // global tenant filter should make this invisible (404, not 403) to any othertenant-admin request.
        var crossTenantProbeAssistant = new AssistantDefinition(
            CrossTenantProbeAssistantId, DefaultTenantId, "Cross-Tenant Isolation Probe", AssistantType.General);

        context.Tenants.Add(otherTenant);
        context.Roles.Add(otherTenantAdminRole);
        context.Users.Add(otherTenantAdminUser);
        context.UserRoles.Add(otherTenantAdminUserRole);
        context.AssistantDefinitions.Add(crossTenantProbeAssistant);

        await context.SaveChangesAsync(cancellationToken);
    }
}