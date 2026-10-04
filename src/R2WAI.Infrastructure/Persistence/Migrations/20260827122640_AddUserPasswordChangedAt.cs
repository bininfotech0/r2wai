using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPasswordChangedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill: every existing row's password was in fact set at CreatedAt (there's no
            // earlier date to know), so a fresh maxPasswordAgeDays policy can evaluate every
            // existing user meaningfully from day one instead of leaving them all at "unknown, so
            // treat as not overdue" indefinitely.
            migrationBuilder.Sql(
                """UPDATE "Users" SET "PasswordChangedAt" = "CreatedAt" WHERE "PasswordChangedAt" IS NULL AND "PasswordHash" IS NOT NULL""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "Users");
        }
    }
}
