using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrichAssistantDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "AssistantDefinitions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishStatus",
                table: "AssistantDefinitions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "AssistantDefinitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublishedVersion",
                table: "AssistantDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "AssistantDefinitions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsageCount",
                table: "AssistantDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantDefinitions_TenantId_PublishStatus",
                table: "AssistantDefinitions",
                columns: new[] { "TenantId", "PublishStatus" });

            migrationBuilder.Sql(
                """UPDATE "AssistantDefinitions" SET "PublishStatus" = 'Published', "PublishedVersion" = 1, "PublishedAt" = NOW() WHERE "IsActive" = true""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssistantDefinitions_TenantId_PublishStatus",
                table: "AssistantDefinitions");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "AssistantDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishStatus",
                table: "AssistantDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "AssistantDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedVersion",
                table: "AssistantDefinitions");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "AssistantDefinitions");

            migrationBuilder.DropColumn(
                name: "UsageCount",
                table: "AssistantDefinitions");
        }
    }
}
