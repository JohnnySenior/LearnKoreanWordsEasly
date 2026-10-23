using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DailyReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WeekdayReminderTime",
                table: "users",
                newName: "ReminderTime");

            migrationBuilder.AddColumn<int>(
                name: "ReminderDays",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 127); // every day (spec §8)

            // The old default was Monday–Friday 09:30 (D-16); spec §8 makes it 20:00 every day.
            migrationBuilder.Sql("UPDATE users SET \"ReminderTime\" = '20:00' WHERE \"ReminderTime\" = '09:30';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderDays",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "ReminderTime",
                table: "users",
                newName: "WeekdayReminderTime");
        }
    }
}
