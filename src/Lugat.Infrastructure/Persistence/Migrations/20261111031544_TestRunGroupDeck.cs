using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestRunGroupDeck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupDeckId",
                table: "test_runs",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupDeckId",
                table: "test_runs");
        }
    }
}
