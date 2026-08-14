using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R2WAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrichModelConfigurationWithGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "ModelConfigurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataClassification",
                table: "ModelConfigurations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Internal");

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "ModelConfigurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelConfigurations_ApplicationId",
                table: "ModelConfigurations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelConfigurations_DepartmentId",
                table: "ModelConfigurations",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModelConfigurations_Applications_ApplicationId",
                table: "ModelConfigurations",
                column: "ApplicationId",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ModelConfigurations_Departments_DepartmentId",
                table: "ModelConfigurations",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModelConfigurations_Applications_ApplicationId",
                table: "ModelConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelConfigurations_Departments_DepartmentId",
                table: "ModelConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ModelConfigurations_ApplicationId",
                table: "ModelConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ModelConfigurations_DepartmentId",
                table: "ModelConfigurations");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "ModelConfigurations");

            migrationBuilder.DropColumn(
                name: "DataClassification",
                table: "ModelConfigurations");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "ModelConfigurations");
        }
    }
}
