using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnsureGlobalWebhookSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "WebhookEndpoints"
                        WHERE "IsDeleted" = FALSE
                        GROUP BY "Slug" HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce globally unique webhook slugs: duplicate active slugs exist. Rename one endpoint per duplicate slug, then retry the migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEndpoints_Slug",
                table: "WebhookEndpoints",
                column: "Slug",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookEndpoints_Slug",
                table: "WebhookEndpoints");
        }
    }
}
