using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupDeckDue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                table: "group_decks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueOn",
                table: "group_decks",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_decks_DueAt",
                table: "group_decks",
                column: "DueAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_group_decks_DueAt",
                table: "group_decks");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "group_decks");

            migrationBuilder.DropColumn(
                name: "DueOn",
                table: "group_decks");
        }
    }
}
