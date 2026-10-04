using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatbotPublishedAssistantVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublishedAssistantVersionId",
                table: "Chatbots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublishedAssistantVersionNumber",
                table: "Chatbots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Chatbots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chatbots_PublishedAssistantVersionId",
                table: "Chatbots",
                column: "PublishedAssistantVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Chatbots_AssistantVersions_PublishedAssistantVersionId",
                table: "Chatbots",
                column: "PublishedAssistantVersionId",
                principalTable: "AssistantVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chatbots_AssistantVersions_PublishedAssistantVersionId",
                table: "Chatbots");

            migrationBuilder.DropIndex(
                name: "IX_Chatbots_PublishedAssistantVersionId",
                table: "Chatbots");

            migrationBuilder.DropColumn(
                name: "PublishedAssistantVersionId",
                table: "Chatbots");

            migrationBuilder.DropColumn(
                name: "PublishedAssistantVersionNumber",
                table: "Chatbots");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Chatbots");
        }
    }
}
