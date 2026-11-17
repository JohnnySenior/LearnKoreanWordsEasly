using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrapWhy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrapWhy",
                table: "personal_words",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrapWhy",
                table: "group_deck_words",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrapWhy",
                table: "catalog_words",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrapWhy",
                table: "personal_words");

            migrationBuilder.DropColumn(
                name: "TrapWhy",
                table: "group_deck_words");

            migrationBuilder.DropColumn(
                name: "TrapWhy",
                table: "catalog_words");
        }
    }
}
