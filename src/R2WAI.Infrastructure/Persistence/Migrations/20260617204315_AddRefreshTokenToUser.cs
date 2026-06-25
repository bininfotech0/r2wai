using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ElsaInstanceId",
                table: "WorkflowInstances",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshTokenHash",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbedScript",
                table: "Chatbots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WidgetSettings",
                table: "Chatbots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ElsaBookmarkId",
                table: "ApprovalRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ElsaInstanceId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmbedScript",
                table: "Chatbots");

            migrationBuilder.DropColumn(
                name: "WidgetSettings",
                table: "Chatbots");

            migrationBuilder.DropColumn(
                name: "ElsaBookmarkId",
                table: "ApprovalRequests");
        }
    }
}
