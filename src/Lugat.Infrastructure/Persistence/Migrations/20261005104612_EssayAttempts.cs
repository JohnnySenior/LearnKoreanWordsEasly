using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EssayAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "essay_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Task = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Source = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Prompt = table.Column<string>(type: "jsonb", nullable: false),
                    Answers = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Feedback = table.Column<string>(type: "jsonb", nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_essay_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_essay_attempts_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_essay_attempts_OwnerId_CreatedAt",
                table: "essay_attempts",
                columns: new[] { "OwnerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "essay_attempts");
        }
    }
}
