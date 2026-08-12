using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantLinkToChatbots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssistantId",
                table: "Chatbots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chatbots_AssistantId",
                table: "Chatbots",
                column: "AssistantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Chatbots_AssistantDefinitions_AssistantId",
                table: "Chatbots",
                column: "AssistantId",
                principalTable: "AssistantDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chatbots_AssistantDefinitions_AssistantId",
                table: "Chatbots");

            migrationBuilder.DropIndex(
                name: "IX_Chatbots_AssistantId",
                table: "Chatbots");

            migrationBuilder.DropColumn(
                name: "AssistantId",
                table: "Chatbots");
        }
    }
}
