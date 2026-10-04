using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.Authentication;

namespace R2WAI.Infrastructure.Persistence;

public sealed record BootstrapAdmin(string? Email, string? Password)
{
    public static BootstrapAdmin From(IConfiguration configuration) =>
        new(configuration["Bootstrap:AdminEmail"], configuration["Bootstrap:AdminPassword"]);
}

public enum BootstrapResult
{
    /// <summary>The database already had a tenant; nothing was changed.</summary>
    AlreadyInitialized,
    CreatedWithAdmin,
    /// <summary>Tenant, roles and built-in tools were created, but no administrator (none configured, or the configured one was rejected).</summary>
    CreatedWithoutAdmin,
}

/// <summary>
/// First-run setup for a real deployment. The full demo seed (ApplicationDbContextSeed) creates accounts
/// with passwords that are published in this repository — admin@r2wai.io holding Admin AND SystemAdmin, a
/// standard user, a department admin, and a second tenant with its own admin — and it used to run on ANY
/// empty database in every environment except "Testing". It is now opt-in (Database:SeedDemoData; on by
/// default only in Development, and set explicitly in the dev docker-compose). Everything else gets this:
/// the default tenant, the three system roles, the built-in tool rows, and — only when configured — one
/// administrator whose email and password come from Bootstrap:AdminEmail / Bootstrap:AdminPassword.
/// </summary>
public static class DatabaseBootstrap
{
    public const int MinimumPasswordLength = 12;

    // Same identifiers as ApplicationDbContextSeed, so the two paths produce the same tenant/roles.
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid UserRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid SystemAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000007");
    private static readonly Guid DefaultModelId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // Published in this repository; never acceptable for a real administrator.
    private static readonly HashSet<string> KnownDemoPasswords = new(StringComparer.Ordinal)
    {
        "R2wai_Admin!2026", "R2wai_User!2026", "R2wai_DeptAdmin!2026", "R2wai_OtherTenant!2026", "Test@1234!",
    };

    public static bool ShouldSeedDemoData(IHostEnvironment environment, IConfiguration configuration) =>
        bool.TryParse(configuration["Database:SeedDemoData"], out var explicitChoice)
            ? explicitChoice
            : environment.IsDevelopment();

    /// <summary>Null when the administrator is acceptable; otherwise why it was rejected (never includes the password).</summary>
    public static string? ValidateAdmin(BootstrapAdmin admin)
    {
        if (string.IsNullOrWhiteSpace(admin.Email) && string.IsNullOrEmpty(admin.Password))
            return "not configured";

        if (string.IsNullOrWhiteSpace(admin.Email) || !new EmailAddressAttribute().IsValid(admin.Email))
            return "Bootstrap:AdminEmail is missing or not a valid email address";

        if (string.IsNullOrEmpty(admin.Password) || admin.Password.Length < MinimumPasswordLength)
            return $"Bootstrap:AdminPassword must be at least {MinimumPasswordLength} characters";

        if (KnownDemoPasswords.Contains(admin.Password))
            return "Bootstrap:AdminPassword is one of the publicly known demo passwords";

        return null;
    }

    public static async Task<BootstrapResult> SeedAsync(
        ApplicationDbContext context, BootstrapAdmin admin, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await context.Tenants.AnyAsync(cancellationToken))
            return BootstrapResult.AlreadyInitialized;

        var tenant = new Tenant(DefaultTenantId, "Default", "default");
        tenant.UpdateFeatures(
            """{"chat":true,"documents":true,"knowledge":true,"chatbots":true,"proposals":true,"workflows":true,"assistants":true}""");
        tenant.UpdateSettings(
            """{"maxUsers":100,"maxStorageMb":5000,"maxDocuments":1000}""");

        var adminRole = new Role(AdminRoleId, DefaultTenantId, "Admin", "System administrator with full access", true);
        adminRole.SetPermissions(Permission.All.ToString());

        var systemAdminRole = new Role(SystemAdminRoleId, DefaultTenantId, "SystemAdmin", "Platform-wide super administrator", true);
        systemAdminRole.SetPermissions(Permission.All.ToString());

        var userRole = new Role(UserRoleId, DefaultTenantId, "User", "Standard user with basic access", true);
        userRole.SetPermissions(
            (Permission.ConversationRead | Permission.ConversationSend | Permission.DocumentRead |
             Permission.DocumentUpload | Permission.KnowledgeBaseRead | Permission.ChatbotRead).ToString());

        var defaultModel = new ModelConfiguration(
            DefaultModelId, DefaultTenantId, "GPT-4o", "OpenAI", "gpt-4o", null, null);
        defaultModel.UpdateDetails("GPT-4o", "OpenAI", "gpt-4o", 8192, 0.7, 1.0);
        defaultModel.SetDefault(true);
        defaultModel.Activate();

        context.Tenants.Add(tenant);
        context.Roles.AddRange(adminRole, userRole, systemAdminRole);
        context.ModelConfigurations.Add(defaultModel);

        // The built-in tool rows, so an administrator can review/tune their governance in the UI (the
        // governance filter also falls back to the same defaults in code for a tenant without rows).
        foreach (var name in BuiltInToolGovernance.KnownFunctionNames)
            context.ToolDefinitions.Add(BuiltInToolGovernance.TryCreateDefault(name, DefaultTenantId)!);

        var problem = ValidateAdmin(admin);
        var result = BootstrapResult.CreatedWithoutAdmin;
        if (problem is null)
        {
            var user = new User(AdminUserId, DefaultTenantId, admin.Email!.Trim(), admin.Email.Trim(), "System", "Administrator");
            user.SetPasswordHash(new PasswordHasher().Hash(admin.Password!));
            context.Users.Add(user);
            context.UserRoles.AddRange(new UserRole(AdminUserId, AdminRoleId), new UserRole(AdminUserId, SystemAdminRoleId));
            result = BootstrapResult.CreatedWithAdmin;
            logger.LogInformation("Bootstrap administrator {Email} created. Remove Bootstrap__AdminPassword from the environment now.", user.Email);
        }
        else if (problem == "not configured")
        {
            logger.LogWarning(
                "No administrator was created: set Bootstrap__AdminEmail and Bootstrap__AdminPassword (>= {Min} characters) and restart against an empty database, or create one directly.",
                MinimumPasswordLength);
        }
        else
        {
            logger.LogError("No administrator was created: {Problem}.", problem);
        }

        await context.SaveChangesAsync(cancellationToken);
        return result;
    }
}
