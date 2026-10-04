using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Widening only: a connected system may be created without an organisational container
    /// (R2WAI 2.0 product decision). No row is inserted, updated or deleted — existing
    /// <c>DepartmentId</c> values are preserved verbatim.
    /// </remarks>
    public partial class MakeConnectedApplicationDepartmentOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                table: "Applications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Rolling back requires every connected system to have a department again. Any NULL row
        /// is deliberately left untouched and reported, rather than being rewritten to the empty
        /// Guid: that value is not a real <c>Departments</c> row, so assigning it would either
        /// break the foreign key or invent an organisation. Resolve the rows first, then roll back.
        /// The column is restored to NOT NULL with no default, matching the pre-migration schema.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Applications" WHERE "DepartmentId" IS NULL) THEN
                        RAISE EXCEPTION
                            'Cannot roll back MakeConnectedApplicationDepartmentOptional: % connected system(s) have no DepartmentId. Assign a department to each, or delete those rows, before reverting.',
                            (SELECT count(*) FROM "Applications" WHERE "DepartmentId" IS NULL);
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                table: "Applications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
