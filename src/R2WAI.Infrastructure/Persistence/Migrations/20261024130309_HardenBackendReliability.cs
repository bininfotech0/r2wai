using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenBackendReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastScheduleOccurrenceAt",
                table: "Workflows",
                type: "timestamp with time zone",
                nullable: true);

            // Start catch-up from deployment time for schedules that already exist. This avoids
            // replaying an unbounded history on first rollout; later downtime is tracked by cursor.
            migrationBuilder.Sql("""
                UPDATE "Workflows"
                SET "LastScheduleOccurrenceAt" = CURRENT_TIMESTAMP
                WHERE "Trigger" LIKE 'schedule:%';
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingResumeLeaseId",
                table: "WorkflowInstances",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_PendingResumeAt_PendingResumeLeaseExpires~",
                table: "WorkflowInstances",
                columns: new[] { "PendingResumeAt", "PendingResumeLeaseExpiresAt" },
                filter: "\"PendingResumeAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_PendingResumeAt_PendingResumeLeaseExpires~",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "LastScheduleOccurrenceAt",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "PendingResumeLeaseId",
                table: "WorkflowInstances");
        }
    }
}
