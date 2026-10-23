using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnitTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OpenedUnitId",
                table: "test_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassMark",
                table: "test_runs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Passed",
                table: "test_runs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "test_runs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Practice"); // every earlier test was a practice test

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "test_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_test_runs_OwnerId_UnitId_Purpose_CreatedAt",
                table: "test_runs",
                columns: new[] { "OwnerId", "UnitId", "Purpose", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_test_runs_UnitId",
                table: "test_runs",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_test_runs_units_UnitId",
                table: "test_runs",
                column: "UnitId",
                principalTable: "units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_test_runs_units_UnitId",
                table: "test_runs");

            migrationBuilder.DropIndex(
                name: "IX_test_runs_OwnerId_UnitId_Purpose_CreatedAt",
                table: "test_runs");

            migrationBuilder.DropIndex(
                name: "IX_test_runs_UnitId",
                table: "test_runs");

            migrationBuilder.DropColumn(
                name: "OpenedUnitId",
                table: "test_runs");

            migrationBuilder.DropColumn(
                name: "PassMark",
                table: "test_runs");

            migrationBuilder.DropColumn(
                name: "Passed",
                table: "test_runs");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "test_runs");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "test_runs");
        }
    }
}
