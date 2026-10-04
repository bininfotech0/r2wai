using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowVersioningAndInstanceVersionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkflowVersionNumber",
                table: "WorkflowInstances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AssistantPromptHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantPromptHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantPromptHistories_AssistantDefinitions_AssistantDefi~",
                        column: x => x.AssistantDefinitionId,
                        principalTable: "AssistantDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ConfigSnapshot = table.Column<string>(type: "text", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowVersions_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantPromptHistories_AssistantDefinitionId_IsActive",
                table: "AssistantPromptHistories",
                columns: new[] { "AssistantDefinitionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantPromptHistories_AssistantDefinitionId_Version",
                table: "AssistantPromptHistories",
                columns: new[] { "AssistantDefinitionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowVersions_WorkflowId_VersionNumber",
                table: "WorkflowVersions",
                columns: new[] { "WorkflowId", "VersionNumber" },
                unique: true);

            // Backfill: one WorkflowVersion snapshot per existing Workflow, capturing its current
            // content under its current Version number — the "one history row per workflow at its
            // current version" baseline the Track B Phase 4b plan calls for. IsPublished mirrors
            // VersionStatus so an already-published workflow's backfilled snapshot starts published too.
            migrationBuilder.Sql("""
                INSERT INTO "WorkflowVersions"
                    ("Id", "TenantId", "WorkflowId", "VersionNumber", "ConfigSnapshot", "IsPublished", "PublishedAt", "CreatedAt", "IsDeleted")
                SELECT
                    gen_random_uuid(), w."TenantId", w."Id", w."Version",
                    json_build_object('Name', w."Name", 'Description', w."Description", 'Type', w."Type", 'Trigger', w."Trigger", 'Steps', w."Steps")::text,
                    (w."VersionStatus" = 'Published'),
                    CASE WHEN w."VersionStatus" = 'Published' THEN COALESCE(w."ModifiedAt", w."CreatedAt") ELSE NULL END,
                    w."CreatedAt", false
                FROM "Workflows" w;
                """);

            // Backfill: existing WorkflowInstance rows resolve against the workflow's current version —
            // the only version we have real content for. Their true historical version was never
            // recorded before this migration, so this is a documented approximation, not a recovered fact.
            migrationBuilder.Sql("""
                UPDATE "WorkflowInstances" wi
                SET "WorkflowVersionNumber" = w."Version"
                FROM "Workflows" w
                WHERE wi."WorkflowId" = w."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantPromptHistories");

            migrationBuilder.DropTable(
                name: "WorkflowVersions");

            migrationBuilder.DropColumn(
                name: "WorkflowVersionNumber",
                table: "WorkflowInstances");
        }
    }
}
