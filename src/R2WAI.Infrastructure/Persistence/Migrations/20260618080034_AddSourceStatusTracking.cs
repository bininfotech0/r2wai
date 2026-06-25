using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceStatusTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChunkCount",
                table: "KnowledgeBaseSources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Error",
                table: "KnowledgeBaseSources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IndexedAt",
                table: "KnowledgeBaseSources",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChunkCount",
                table: "KnowledgeBaseSources");

            migrationBuilder.DropColumn(
                name: "Error",
                table: "KnowledgeBaseSources");

            migrationBuilder.DropColumn(
                name: "IndexedAt",
                table: "KnowledgeBaseSources");
        }
    }
}
