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
        adminUser.SetPasswordHash(new PasswordHasher().Hash("R2wai_Admin!2026"));

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

        var wordPressAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000105"),
            DefaultTenantId, "WordPress Assistant", AssistantType.WordPress,
            DefaultModelId, null);
        wordPressAssistant.UpdateDetails("WordPress Assistant",
            "Helps with WordPress SEO, plugin/theme guidance, content publishing, and site maintenance.",
            R2WAI.Infrastructure.AI.Prompts.SystemPromptTemplates.GetTemplate(AssistantType.WordPress),
            null, null);
        wordPressAssistant.Publish();

        var strapiAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000106"),
            DefaultTenantId, "Strapi Assistant", AssistantType.Strapi,
            DefaultModelId, null);
        strapiAssistant.UpdateDetails("Strapi Assistant",
            "Helps with Strapi content-type modeling, roles/permissions, API tokens, and REST/GraphQL queries.",
            R2WAI.Infrastructure.AI.Prompts.SystemPromptTemplates.GetTemplate(AssistantType.Strapi),
            null, null);
        strapiAssistant.Publish();

        var joomlaAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000107"),
            DefaultTenantId, "Joomla Assistant", AssistantType.Joomla,
            DefaultModelId, null);
        joomlaAssistant.UpdateDetails("Joomla Assistant",
            "Helps with Joomla articles, menus, extensions, user ACL, and SEF/SEO configuration.",
            R2WAI.Infrastructure.AI.Prompts.SystemPromptTemplates.GetTemplate(AssistantType.Joomla),
            null, null);
        joomlaAssistant.Publish();

        var drupalAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000108"),
            DefaultTenantId, "Drupal Assistant", AssistantType.Drupal,
            DefaultModelId, null);
        drupalAssistant.UpdateDetails("Drupal Assistant",
            "Helps with Drupal content types, taxonomy, Views, modules, and roles/permissions.",
            R2WAI.Infrastructure.AI.Prompts.SystemPromptTemplates.GetTemplate(AssistantType.Drupal),
            null, null);
        drupalAssistant.Publish();

        var shopifyAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000109"),
            DefaultTenantId, "Shopify Assistant", AssistantType.Shopify,
            DefaultModelId, null);
        shopifyAssistant.UpdateDetails("Shopify Assistant",
            "Helps with Shopify products, theme/Liquid basics, apps, discounts, and order management.",
            R2WAI.Infrastructure.AI.Prompts.SystemPromptTemplates.GetTemplate(AssistantType.Shopify),
            null, null);
        shopifyAssistant.Publish();

        var salesforceAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000110"),
            DefaultTenantId, "Salesforce Assistant", AssistantType.Salesforce,
            DefaultModelId, null);
        salesforceAssistant.UpdateDetails("Salesforce Assistant",
            "Helps with leads/opportunities, reports and dashboards, flows, and roles/permission sets.",
            R2WAI.Infrastructure.AI.Prompts.SystemPromptTemplates.GetTemplate(AssistantType.Salesforce),
            null, null);
        salesforceAssistant.Publish();

        var sapAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000111"),
            DefaultTenantId, "SAP Assistant", AssistantType.SAP,
            DefaultModelId, null);
        sapAssistant.UpdateDetails("SAP Assistant",
            "Helps with SAP T-codes, master data, core modules (FI/CO, MM, SD), and approval workflows.",
            R2WAI.Infrastructure.AI.Prompts.SystemPromptTemplates.GetTemplate(AssistantType.SAP),
            null, null);
        sapAssistant.Publish();

        context.AssistantDefinitions.AddRange(
            wordPressAssistant, strapiAssistant, joomlaAssistant, drupalAssistant,
            shopifyAssistant, salesforceAssistant, sapAssistant);

        // --- Coaching Center Admissions Assistant (EdTech demo) ---
        var coachingKnowledgeBase = new KnowledgeBase(
            Guid.Parse("00000000-0000-0000-0000-000000000501"),
            DefaultTenantId, AdminUserId, "Coaching Center FAQs",
            "Admissions, fees, batch timings, and exam-update knowledge base for the coaching center demo.");
        coachingKnowledgeBase.UpdateStatus(KnowledgeBaseStatus.Active);

        var coachingKnowledgeSource = new KnowledgeBaseSource(
            Guid.Parse("00000000-0000-0000-0000-000000000502"),
            coachingKnowledgeBase.Id, "text", null, null,
            """
            Admissions: Batches run for Classes 8-12, JEE/NEET foundation, and JSSC/JPSC competitive exam coaching.
            New batches start on the 1st and 15th of every month. Admission requires a filled form, previous marksheet copy, and the first installment.
            Fees: Foundation batches Rs. 3,500/month, JEE/NEET batches Rs. 6,000/month, JSSC/JPSC batches Rs. 4,500/month. Sibling discount 10%.
            Fees are due by the 5th of each month; a late fee of Rs. 200 applies after the 10th. Payment link is sent via WhatsApp.
            Demo classes: Every student gets one free demo class before enrolling. Demo slots are available Mon-Sat, 4 PM-7 PM.
            Timings: Morning batch 6 AM-8 AM, evening batches 4 PM-9 PM. Sunday doubt-clearing session 10 AM-1 PM.
            Results & exams: JSSC/JPSC exam dates, admit card releases, and cutoff updates are broadcast to all enrolled students via WhatsApp as soon as official notifications are out.
            Location: Bistupur, Jamshedpur, Jharkhand. Contact: admissions@coachingcenter.example, +91-90000-00000.
            """);
        coachingKnowledgeSource.MarkIndexed(1);
        coachingKnowledgeBase.AddSource(coachingKnowledgeSource);
        coachingKnowledgeBase.IncrementDocumentCount();

        var coachingAssistant = new AssistantDefinition(
            Guid.Parse("00000000-0000-0000-0000-000000000104"),
            DefaultTenantId, "Coaching Center Admissions Assistant", AssistantType.CoachingCenter,
            DefaultModelId, coachingKnowledgeBase.Id);
        coachingAssistant.UpdateDetails("Coaching Center Admissions Assistant",
            "Answers admission, fee, batch, and exam queries for a coaching center and captures student leads.",
            "You are an admissions assistant for a coaching center in Jamshedpur, Jharkhand. Answer questions about courses, batch timings, fees, and demo-class booking in simple Hindi or English, whichever the student uses. When a prospective student shares their name, class, phone number, or subject interest, acknowledge it warmly and let them know a counsellor will follow up. Reference exam dates and results (JSSC/JPSC) from the knowledge base when asked.",
            null, null,
            tags: """["coaching","admissions","edtech","jharkhand"]""");
        coachingAssistant.Publish();

        context.KnowledgeBases.Add(coachingKnowledgeBase);
        context.AssistantDefinitions.AddRange(hrAssistant, itAssistant, financeAssistant, coachingAssistant);

        var admissionsChatbot = new Chatbot(
            Guid.Parse("00000000-0000-0000-0000-000000000401"),
            DefaultTenantId, AdminUserId, "Admission Enquiry Bot",
            coachingKnowledgeBase.Id, DefaultModelId);
        admissionsChatbot.UpdateDetails(
            "Admission Enquiry Bot",
            "Embeddable widget answering admission, fee, and batch questions and capturing student leads 24/7.",
            "Namaste! Ask me about courses, fees, batch timings, or book a free demo class.",
            """["What courses do you offer?","What are the fees for JEE/NEET batches?","How do I book a free demo class?","When is the next JSSC exam?"]""",
            "You are an admissions assistant for a coaching center in Jamshedpur, Jharkhand. Answer questions about courses, batch timings, fees, and demo-class booking in simple Hindi or English, whichever the student uses.");
        admissionsChatbot.UpdateStatus(ChatbotStatus.Active);

        context.Chatbots.Add(admissionsChatbot);

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

        // --- Sample Leads (coaching center demo) ---
        var sampleLead1 = new Lead(
            Guid.Parse("00000000-0000-0000-0000-000000000601"),
            DefaultTenantId, "Aarav Kumar", admissionsChatbot.Id,
            "+91-98765-43210", null, "Class 10", "JEE Foundation batch", "chatbot");
        sampleLead1.UpdateStatus(LeadStatus.DemoScheduled);

        var sampleLead2 = new Lead(
            Guid.Parse("00000000-0000-0000-0000-000000000602"),
            DefaultTenantId, "Priya Singh", admissionsChatbot.Id,
            "+91-91234-56780", null, "Class 12", "NEET batch fees", "chatbot");
        sampleLead2.UpdateStatus(LeadStatus.New);

        var sampleLead3 = new Lead(
            Guid.Parse("00000000-0000-0000-0000-000000000603"),
            DefaultTenantId, "Rohit Mahato", admissionsChatbot.Id,
            "+91-99887-76655", null, "JSSC Aspirant", "JSSC/JPSC batch admission", "chatbot");
        sampleLead3.UpdateStatus(LeadStatus.Enrolled);

        context.Leads.AddRange(sampleLead1, sampleLead2, sampleLead3);

        // --- Demo Approval Policy ---
        var approvalPolicy = new ApprovalPolicy(
            Guid.Parse("00000000-0000-0000-0000-000000000301"),
            DefaultTenantId, "Default Approval Policy",
            "Standard approval chain for all workflows",
            null, """["Admin","WorkflowManager"]""", 1, 60, """["Admin"]""");

        context.ApprovalPolicies.Add(approvalPolicy);

        await context.SaveChangesAsync(cancellationToken);
    }
}