using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpServerConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "McpServerConnectionId",
                table: "ToolDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "McpToolName",
                table: "ToolDefinitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "McpServerConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EndpointUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AuthHeaderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CredentialEncrypted = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastTestStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpServerConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpServerConnections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToolDefinitions_McpServerConnectionId",
                table: "ToolDefinitions",
                column: "McpServerConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_McpServerConnections_TenantId_Name",
                table: "McpServerConnections",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_ToolDefinitions_McpServerConnections_McpServerConnectionId",
                table: "ToolDefinitions",
                column: "McpServerConnectionId",
                principalTable: "McpServerConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ToolDefinitions_McpServerConnections_McpServerConnectionId",
                table: "ToolDefinitions");

            migrationBuilder.DropTable(
                name: "McpServerConnections");

            migrationBuilder.DropIndex(
                name: "IX_ToolDefinitions_McpServerConnectionId",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "McpServerConnectionId",
                table: "ToolDefinitions");

            migrationBuilder.DropColumn(
                name: "McpToolName",
                table: "ToolDefinitions");
        }
    }
}
