using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationLinkToAssistantsKnowledgeWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "Workflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "KnowledgeBases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "AssistantDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_ApplicationId",
                table: "Workflows",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBases_ApplicationId",
                table: "KnowledgeBases",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantDefinitions_ApplicationId",
                table: "AssistantDefinitions",
                column: "ApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantDefinitions_Applications_ApplicationId",
                table: "AssistantDefinitions",
                column: "ApplicationId",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeBases_Applications_ApplicationId",
                table: "KnowledgeBases",
                column: "ApplicationId",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Workflows_Applications_ApplicationId",
                table: "Workflows",
                column: "ApplicationId",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssistantDefinitions_Applications_ApplicationId",
                table: "AssistantDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeBases_Applications_ApplicationId",
                table: "KnowledgeBases");

            migrationBuilder.DropForeignKey(
                name: "FK_Workflows_Applications_ApplicationId",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_ApplicationId",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBases_ApplicationId",
                table: "KnowledgeBases");

            migrationBuilder.DropIndex(
                name: "IX_AssistantDefinitions_ApplicationId",
                table: "AssistantDefinitions");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "KnowledgeBases");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "AssistantDefinitions");
        }
    }
}
