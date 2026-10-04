using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data-only migration (no schema change): collapses the 6 system RBAC roles down to the 3 the
    /// nav (roleNav.ts) already implies — Admin/User/SystemAdmin — by folding Editor/Contributor/
    /// WorkflowManager/UserManager into Admin. Scoped to IsSystem=true rows only, so a tenant's own
    /// custom role (CreateRoleCommand, always IsSystem=false) sharing one of these names is never
    /// touched. Every affected UserRoles row is logged to a scratch audit table first so Down() can
    /// genuinely restore it, not just fake a reversal.
    /// </summary>
    public partial class CollapseRbacToThreeRoles : Migration
    {
        private const string RetiringRoleNames = "'Editor', 'Contributor', 'WorkflowManager', 'UserManager'";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE "_RbacCollapseAudit20260829" (
                    "UserId" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "OldRoleId" uuid NOT NULL,
                    "OldRoleName" text NOT NULL,
                    "HadAdminAlready" boolean NOT NULL
                );
                """);

            // Log every affected UserRoles row before touching anything — this is what Down() replays.
            migrationBuilder.Sql($"""
                INSERT INTO "_RbacCollapseAudit20260829" ("UserId", "TenantId", "OldRoleId", "OldRoleName", "HadAdminAlready")
                SELECT ur."UserId", retiring."TenantId", retiring."Id", retiring."Name",
                       EXISTS (
                           SELECT 1 FROM "UserRoles" ur2
                           JOIN "Roles" adminRole ON adminRole."Id" = ur2."RoleId"
                           WHERE ur2."UserId" = ur."UserId"
                             AND adminRole."TenantId" = retiring."TenantId"
                             AND adminRole."Name" = 'Admin' AND adminRole."IsSystem" = true
                       )
                FROM "UserRoles" ur
                JOIN "Roles" retiring ON retiring."Id" = ur."RoleId"
                WHERE retiring."Name" IN ({RetiringRoleNames}) AND retiring."IsSystem" = true;
                """);

            // A user who already holds both a retiring role and Admin in the same tenant: just drop
            // the now-redundant retiring-role row (they already have equivalent access).
            migrationBuilder.Sql($"""
                DELETE FROM "UserRoles" ur
                USING "Roles" retiring
                WHERE ur."RoleId" = retiring."Id"
                  AND retiring."Name" IN ({RetiringRoleNames}) AND retiring."IsSystem" = true
                  AND EXISTS (
                      SELECT 1 FROM "UserRoles" ur2
                      JOIN "Roles" adminRole ON adminRole."Id" = ur2."RoleId"
                      WHERE ur2."UserId" = ur."UserId"
                        AND adminRole."TenantId" = retiring."TenantId"
                        AND adminRole."Name" = 'Admin' AND adminRole."IsSystem" = true
                  );
                """);

            // Everyone else holding a retiring role: repoint to that tenant's Admin role.
            migrationBuilder.Sql($"""
                UPDATE "UserRoles" ur
                SET "RoleId" = adminRole."Id"
                FROM "Roles" retiring, "Roles" adminRole
                WHERE ur."RoleId" = retiring."Id"
                  AND retiring."Name" IN ({RetiringRoleNames}) AND retiring."IsSystem" = true
                  AND adminRole."TenantId" = retiring."TenantId"
                  AND adminRole."Name" = 'Admin' AND adminRole."IsSystem" = true;
                """);

            // Delete the retired role rows — guarded to only remove rows with zero remaining
            // UserRoles references, so a tenant with retiring-role holders but no Admin role (an
            // edge case the standard seed never produces) is left with a harmless intact row
            // instead of failing on an FK violation.
            migrationBuilder.Sql($"""
                DELETE FROM "Roles" r
                WHERE r."Name" IN ({RetiringRoleNames}) AND r."IsSystem" = true
                  AND NOT EXISTS (SELECT 1 FROM "UserRoles" ur WHERE ur."RoleId" = r."Id");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate the 4 retired roles with their original well-known seed IDs/names/descriptions
            // (ApplicationDbContextSeed.cs), only where they don't already exist (idempotent).
            // "Permissions" is deliberately left NULL rather than reconstructing the comma-separated
            // Permission-flags string by hand: User.HasPermission() (its only reader) is dead code —
            // never called anywhere in the app — so faking a plausible-looking value here would be
            // worse than an honest NULL for anyone who later wires that method up for real.
            migrationBuilder.Sql("""
                INSERT INTO "Roles" ("Id", "TenantId", "Name", "Description", "Permissions", "IsSystem", "CreatedAt", "ModifiedAt")
                SELECT DISTINCT a."OldRoleId", a."TenantId", a."OldRoleName",
                    CASE a."OldRoleName"
                        WHEN 'Editor' THEN 'Can manage documents and content'
                        WHEN 'Contributor' THEN 'Can contribute documents'
                        WHEN 'WorkflowManager' THEN 'Can manage workflows'
                        WHEN 'UserManager' THEN 'Can manage users'
                    END,
                    NULL, true, now(), NULL
                FROM "_RbacCollapseAudit20260829" a
                WHERE NOT EXISTS (SELECT 1 FROM "Roles" r WHERE r."Id" = a."OldRoleId");
                """);

            // Restore each logged UserRoles row pointing at the original retiring role. Harmless if
            // the user also still holds Admin (composite PK just needs the row to exist).
            migrationBuilder.Sql("""
                INSERT INTO "UserRoles" ("UserId", "RoleId")
                SELECT DISTINCT a."UserId", a."OldRoleId"
                FROM "_RbacCollapseAudit20260829" a
                WHERE NOT EXISTS (
                    SELECT 1 FROM "UserRoles" ur WHERE ur."UserId" = a."UserId" AND ur."RoleId" = a."OldRoleId"
                );
                """);

            migrationBuilder.Sql("""DROP TABLE IF EXISTS "_RbacCollapseAudit20260829";""");
        }
    }
}
