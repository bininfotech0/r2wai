using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Deliberately a no-op migration: "xmin" is Postgres's built-in system column, present on every
    /// table already — Postgres rejects ADD COLUMN for a name it reserves as a system column, so the
    /// scaffolded AddColumn/DropColumn calls here were removed by hand. This migration exists purely
    /// so EF's migration history/model snapshot records the new shadow property (AssistantDefinition/
    /// ConnectedApplication/Workflow now use xmin as an IsRowVersion() concurrency token) — no DDL
    /// actually runs, on Up or Down.
    /// </summary>
    public partial class AddPublishConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
