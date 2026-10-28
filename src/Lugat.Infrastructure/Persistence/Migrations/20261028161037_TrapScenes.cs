using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrapScenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrapCue",
                table: "personal_words",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrapRight",
                table: "personal_words",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrapWrong",
                table: "personal_words",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrapCue",
                table: "catalog_words",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrapRight",
                table: "catalog_words",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrapSceneAskedAt",
                table: "catalog_words",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrapScenePending",
                table: "catalog_words",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TrapWrong",
                table: "catalog_words",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrapCue",
                table: "personal_words");

            migrationBuilder.DropColumn(
                name: "TrapRight",
                table: "personal_words");

            migrationBuilder.DropColumn(
                name: "TrapWrong",
                table: "personal_words");

            migrationBuilder.DropColumn(
                name: "TrapCue",
                table: "catalog_words");

            migrationBuilder.DropColumn(
                name: "TrapRight",
                table: "catalog_words");

            migrationBuilder.DropColumn(
                name: "TrapSceneAskedAt",
                table: "catalog_words");

            migrationBuilder.DropColumn(
                name: "TrapScenePending",
                table: "catalog_words");

            migrationBuilder.DropColumn(
                name: "TrapWrong",
                table: "catalog_words");
        }
    }
}
