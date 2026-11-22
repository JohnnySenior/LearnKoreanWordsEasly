using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LevelPlacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KnownUnits",
                table: "test_runs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "test_runs",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KnownUnits",
                table: "test_runs");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "test_runs");
        }
    }
}
