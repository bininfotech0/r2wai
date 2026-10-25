using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnsureUniqueActiveApprovalPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ApprovalPolicies"
                SET "WorkflowType" = NULLIF(BTRIM("WorkflowType"), '')
                WHERE "WorkflowType" IS NOT NULL;
                """);

            // Fail explicitly rather than deactivating or deleting policy data during deployment.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "ApprovalPolicies"
                        WHERE "IsDeleted" = FALSE AND "IsActive" = TRUE AND "WorkflowType" IS NULL
                        GROUP BY "TenantId" HAVING COUNT(*) > 1
                    ) OR EXISTS (
                        SELECT 1 FROM "ApprovalPolicies"
                        WHERE "IsDeleted" = FALSE AND "IsActive" = TRUE AND "WorkflowType" IS NOT NULL
                        GROUP BY "TenantId", "WorkflowType" HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce one active approval policy per workflow type: duplicate active policies exist. Deactivate or consolidate them, then retry the migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_TenantId_WorkflowType",
                table: "ApprovalPolicies");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_TenantId",
                table: "ApprovalPolicies",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"IsActive\" = TRUE AND \"WorkflowType\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_TenantId_WorkflowType",
                table: "ApprovalPolicies",
                columns: new[] { "TenantId", "WorkflowType" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"IsActive\" = TRUE AND \"WorkflowType\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_TenantId",
                table: "ApprovalPolicies");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_TenantId_WorkflowType",
                table: "ApprovalPolicies");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_TenantId_WorkflowType",
                table: "ApprovalPolicies",
                columns: new[] { "TenantId", "WorkflowType" });
        }
    }
}
