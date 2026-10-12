using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Database backstop for the uniqueness the application handlers check (ApplicationUniqueness,
    /// CreateApplicationCommandHandler):
    ///  - the existing Code indexes now skip soft-deleted rows. The handlers' pre-checks run through
    ///    the IsDeleted query filter, so an index that still counted deleted rows let "delete a
    ///    system, re-create one with the same code" pass validation and then fail with a bare 409;
    ///  - case-insensitive lower(...) expression indexes for Name (per tenant) and Code (same two
    ///    scopes as above). EF's HasIndex can't express these, so they are raw SQL and deliberately
    ///    absent from the model snapshot — EF never sees them, so it never tries to drop them.
    /// Names were never unique before this, so a database can already hold live duplicates (exact or
    /// differing only by case), and a failed index build fails startup — DatabaseInitializer rethrows.
    /// Up therefore first renames every live duplicate except the oldest in its scope, appending the
    /// first 8 characters of its own id ("HR Portal (3f2b8c1e)", code "HR_3f2b8c1e"). Unique by
    /// construction, visibly renamed for an admin to tidy, and nothing is deleted.
    /// </summary>
    public partial class CaseInsensitiveApplicationNameAndCodeUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Name is varchar(500): " (" + 8 + ")" is 11 characters.
            migrationBuilder.Sql(
                """
                UPDATE "Applications" AS a
                SET "Name" = left(a."Name", 489) || ' (' || left(a."Id"::text, 8) || ')'
                FROM (
                    SELECT "Id", row_number() OVER (
                        PARTITION BY "TenantId", lower("Name") ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Applications"
                    WHERE "IsDeleted" = false
                ) AS d
                WHERE a."Id" = d."Id" AND d.rn > 1;
                """);

            // Code is varchar(100): "_" + 8 is 9 characters. PARTITION BY groups NULL DepartmentIds
            // together, which is exactly the department-less index's scope.
            migrationBuilder.Sql(
                """
                UPDATE "Applications" AS a
                SET "Code" = left(a."Code", 91) || '_' || left(a."Id"::text, 8)
                FROM (
                    SELECT "Id", row_number() OVER (
                        PARTITION BY "TenantId", "DepartmentId", lower("Code") ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Applications"
                    WHERE "IsDeleted" = false
                ) AS d
                WHERE a."Id" = d."Id" AND d.rn > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Applications_TenantId_Code_NoDepartment",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_TenantId_DepartmentId_Code",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantId_Code_NoDepartment",
                table: "Applications",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"DepartmentId\" IS NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantId_DepartmentId_Code",
                table: "Applications",
                columns: new[] { "TenantId", "DepartmentId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.Sql(
                """CREATE UNIQUE INDEX "IX_Applications_TenantId_LowerName" ON "Applications" ("TenantId", lower("Name")) WHERE "IsDeleted" = false;""");
            migrationBuilder.Sql(
                """CREATE UNIQUE INDEX "IX_Applications_TenantId_DepartmentId_LowerCode" ON "Applications" ("TenantId", "DepartmentId", lower("Code")) WHERE "IsDeleted" = false;""");
            migrationBuilder.Sql(
                """CREATE UNIQUE INDEX "IX_Applications_TenantId_LowerCode_NoDepartment" ON "Applications" ("TenantId", lower("Code")) WHERE "DepartmentId" IS NULL AND "IsDeleted" = false;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Applications_TenantId_LowerCode_NoDepartment";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Applications_TenantId_DepartmentId_LowerCode";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Applications_TenantId_LowerName";""");

            migrationBuilder.DropIndex(
                name: "IX_Applications_TenantId_Code_NoDepartment",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_TenantId_DepartmentId_Code",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantId_Code_NoDepartment",
                table: "Applications",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"DepartmentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantId_DepartmentId_Code",
                table: "Applications",
                columns: new[] { "TenantId", "DepartmentId", "Code" },
                unique: true);
        }
    }
}
