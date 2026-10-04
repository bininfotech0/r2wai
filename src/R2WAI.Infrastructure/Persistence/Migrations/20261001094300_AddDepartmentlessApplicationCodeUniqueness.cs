using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Closes the uniqueness gap that <c>MakeConnectedApplicationDepartmentOptional</c> opened.
    /// That migration made <c>Applications.DepartmentId</c> nullable, and because Postgres treats
    /// NULLs as distinct in a unique index, the existing
    /// <c>(TenantId, DepartmentId, Code)</c> constraint stopped applying to department-less rows.
    /// Two connected systems with no department could then share a Code within a tenant.
    ///
    /// This adds a partial unique index over exactly the previously-unconstrained rows rather than
    /// narrowing the existing one, so department-scoped uniqueness semantics are unchanged.
    ///
    /// <c>Down</c> simply drops the index and is safe: it only re-opens the gap, it never removes
    /// data. The <c>Up</c> preflight exists because a database that had the nullable column in
    /// production before this migration could already contain the duplicates the index forbids.
    /// </remarks>
    public partial class AddDepartmentlessApplicationCodeUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail with an actionable message instead of letting CREATE UNIQUE INDEX raise a bare
            // 23505. Nothing is modified before this check, so a failure leaves the schema as-is.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    offender text;
                BEGIN
                    SELECT string_agg(DISTINCT t."Name" || '/' || a."Code", ', ')
                      INTO offender
                      FROM "Applications" a
                      JOIN "Tenants" t ON t."Id" = a."TenantId"
                     WHERE a."DepartmentId" IS NULL
                     GROUP BY a."TenantId", a."Code"
                    HAVING count(*) > 1
                     LIMIT 1;

                    IF offender IS NOT NULL THEN
                        RAISE EXCEPTION
                            'Cannot add IX_Applications_TenantId_Code_NoDepartment: connected systems without a Department already share a Code (%). Rename the duplicates, assign each a Department, or delete them, then migrate again.',
                            offender;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantId_Code_NoDepartment",
                table: "Applications",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"DepartmentId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_TenantId_Code_NoDepartment",
                table: "Applications");
        }
    }
}