using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Content = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    SourceConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMemories_AssistantDefinitions_AssistantId",
                        column: x => x.AssistantId,
                        principalTable: "AssistantDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMemories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserMemories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMemorySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoLearnEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewLearned = table.Column<bool>(type: "boolean", nullable: false),
                    AgentNotesEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemorySettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMemorySettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserMemorySettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_AssistantId",
                table: "UserMemories",
                column: "AssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_TenantId",
                table: "UserMemories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_UserId_AssistantId_Key",
                table: "UserMemories",
                columns: new[] { "UserId", "AssistantId", "Key" },
                unique: true,
                filter: "\"AssistantId\" IS NOT NULL AND \"Key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_UserId_AssistantId_Status",
                table: "UserMemories",
                columns: new[] { "UserId", "AssistantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_UserId_Key_Personal",
                table: "UserMemories",
                columns: new[] { "UserId", "Key" },
                unique: true,
                filter: "\"AssistantId\" IS NULL AND \"Key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemorySettings_TenantId",
                table: "UserMemorySettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemorySettings_UserId",
                table: "UserMemorySettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMemories");

            migrationBuilder.DropTable(
                name: "UserMemorySettings");
        }
    }
}
