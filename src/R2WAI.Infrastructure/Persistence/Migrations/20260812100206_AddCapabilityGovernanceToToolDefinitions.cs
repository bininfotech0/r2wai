using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityGovernanceToToolDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ToolDefinitions_TenantId_Name",
                table: "ToolDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ToolDefinitions_TenantId_ToolType",
                table: "ToolDefinitions");

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationApiId",
                table: "ToolDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "ToolDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ApprovalRequired",
                table: "ToolDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AuditRequired",
                table: "ToolDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ConfirmationRequired",
                table: "ToolDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EndpointPath",
                table: "ToolDefinitions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HttpMethod",
                table: "ToolDefinitions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredRole",
                table: "ToolDefinitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                table: "ToolDefinitions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Low");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_ApplicationApiId",
                table: "ToolDefinitions",
                column: "ApplicationApiId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_ApplicationId",
                table: "ToolDefinitions",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_TenantId_IsActive",
                table: "ToolDefinitions",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_TenantId_Name",
                table: "ToolDefinitions",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_ToolDefinitions_ApplicationApis_ApplicationApiId",
                table: "ToolDefinitions",
                column: "ApplicationApiId",
                principalTable: "ApplicationApis",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ToolDefinitions_Applications_ApplicationId",
                table: "ToolDefinitions",
                column: "ApplicationId",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ToolDefinitions_ApplicationApis_ApplicationApiId",
                table: "ToolDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_ToolDefinitions_Applications_ApplicationId",
                table: "ToolDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ToolDefinitions_ApplicationApiId",
                table: "ToolDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ToolDefinitions_ApplicationId",
                table: "ToolDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ToolDefinitions_TenantId_IsActive",
                table: "ToolDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ToolDefinitions_TenantId_Name",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "ApplicationApiId",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "ApprovalRequired",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "AuditRequired",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "ConfirmationRequired",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "EndpointPath",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "HttpMethod",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "RequiredRole",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "ToolDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_TenantId_Name",
                table: "ToolDefinitions",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_TenantId_ToolType",
                table: "ToolDefinitions",
                columns: new[] { "TenantId", "ToolType" });
        }
    }
}
