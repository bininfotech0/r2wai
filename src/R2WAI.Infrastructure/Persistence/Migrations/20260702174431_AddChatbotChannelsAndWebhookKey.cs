using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatbotChannelsAndWebhookKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebhookApiKeyHash",
                table: "Chatbots",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookApiKeyPrefix",
                table: "Chatbots",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChatbotChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatbotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedCredentials = table.Column<string>(type: "text", nullable: true),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatbotChannels_Chatbots_ChatbotId",
                        column: x => x.ChatbotId,
                        principalTable: "Chatbots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotChannels_ChatbotId",
                table: "ChatbotChannels",
                column: "ChatbotId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotChannels_TenantId_ChatbotId_ChannelType",
                table: "ChatbotChannels",
                columns: new[] { "TenantId", "ChatbotId", "ChannelType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatbotChannels");

            migrationBuilder.DropColumn(
                name: "WebhookApiKeyHash",
                table: "Chatbots");

            migrationBuilder.DropColumn(
                name: "WebhookApiKeyPrefix",
                table: "Chatbots");
        }
    }
}
