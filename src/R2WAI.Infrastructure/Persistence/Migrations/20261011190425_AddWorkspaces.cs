using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Workflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "KnowledgeBases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Chatbots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "AssistantDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "Applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workspaces_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_TenantId_WorkspaceId",
                table: "Workflows",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_WorkspaceId",
                table: "Workflows",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBases_TenantId_WorkspaceId",
                table: "KnowledgeBases",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBases_WorkspaceId",
                table: "KnowledgeBases",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Chatbots_TenantId_WorkspaceId",
                table: "Chatbots",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Chatbots_WorkspaceId",
                table: "Chatbots",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantDefinitions_TenantId_WorkspaceId",
                table: "AssistantDefinitions",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantDefinitions_WorkspaceId",
                table: "AssistantDefinitions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantId_WorkspaceId",
                table: "Applications",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_WorkspaceId",
                table: "Applications",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_TenantId_Default",
                table: "Workspaces",
                column: "TenantId",
                unique: true,
                filter: "\"IsDefault\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_TenantId_Name",
                table: "Workspaces",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Workspaces_WorkspaceId",
                table: "Applications",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantDefinitions_Workspaces_WorkspaceId",
                table: "AssistantDefinitions",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Chatbots_Workspaces_WorkspaceId",
                table: "Chatbots",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeBases_Workspaces_WorkspaceId",
                table: "KnowledgeBases",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Workflows_Workspaces_WorkspaceId",
                table: "Workflows",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill: one default workspace per tenant (soft-deleted tenants included, so their
            // rows get a workspace too), then every existing resource moves into its tenant's
            // default. Departments are deliberately not mapped to workspaces — they are a separate
            // axis (WORKSPACE-MIGRATION-PLAN §3 Phase 2). Name/description match Workspace.CreateDefault.
            migrationBuilder.Sql(
                """
                INSERT INTO "Workspaces" ("Id", "TenantId", "Name", "Description", "IsDefault", "CreatedByUserId", "CreatedAt", "IsDeleted")
                SELECT gen_random_uuid(), t."Id", 'Default workspace',
                       'Created automatically. Holds everything not assigned to another workspace.',
                       true, NULL, now(), false
                FROM "Tenants" t
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Workspaces" w
                    WHERE w."TenantId" = t."Id" AND w."IsDefault" AND NOT w."IsDeleted");
                """);

            foreach (var table in new[] { "AssistantDefinitions", "KnowledgeBases", "Workflows", "Chatbots", "Applications" })
            {
                migrationBuilder.Sql(
                    $"""
                    UPDATE "{table}" AS r
                    SET "WorkspaceId" = w."Id"
                    FROM "Workspaces" w
                    WHERE r."WorkspaceId" IS NULL
                      AND w."TenantId" = r."TenantId" AND w."IsDefault" AND NOT w."IsDeleted";
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Workspaces_WorkspaceId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantDefinitions_Workspaces_WorkspaceId",
                table: "AssistantDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_Chatbots_Workspaces_WorkspaceId",
                table: "Chatbots");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeBases_Workspaces_WorkspaceId",
                table: "KnowledgeBases");

            migrationBuilder.DropForeignKey(
                name: "FK_Workflows_Workspaces_WorkspaceId",
                table: "Workflows");

            migrationBuilder.DropTable(
                name: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_TenantId_WorkspaceId",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_WorkspaceId",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBases_TenantId_WorkspaceId",
                table: "KnowledgeBases");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBases_WorkspaceId",
                table: "KnowledgeBases");

            migrationBuilder.DropIndex(
                name: "IX_Chatbots_TenantId_WorkspaceId",
                table: "Chatbots");

            migrationBuilder.DropIndex(
                name: "IX_Chatbots_WorkspaceId",
                table: "Chatbots");

            migrationBuilder.DropIndex(
                name: "IX_AssistantDefinitions_TenantId_WorkspaceId",
                table: "AssistantDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AssistantDefinitions_WorkspaceId",
                table: "AssistantDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Applications_TenantId_WorkspaceId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_WorkspaceId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "KnowledgeBases");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Chatbots");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "AssistantDefinitions");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Applications");
        }
    }
}
