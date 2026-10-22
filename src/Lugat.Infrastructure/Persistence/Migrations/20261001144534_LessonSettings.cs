using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LessonSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "LessonDays",
                table: "users",
                type: "integer[]",
                nullable: false,
                defaultValue: new[] { 2, 4, 6 }); // existing learners keep Tue/Thu/Sat (D-43)

            migrationBuilder.AddColumn<int>(
                name: "TablesPerLesson",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LessonDays",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TablesPerLesson",
                table: "users");
        }
    }
}
