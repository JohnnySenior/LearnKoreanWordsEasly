using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WordUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "word_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordSenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    Phrases = table.Column<string>(type: "jsonb", nullable: false),
                    TrapUz = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Example = table.Column<string>(type: "jsonb", nullable: true),
                    Questions = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Warning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_usages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_usages_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_word_usages_word_senses_WordSenseId",
                        column: x => x.WordSenseId,
                        principalTable: "word_senses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_word_usages_OwnerId",
                table: "word_usages",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_word_usages_WordSenseId",
                table: "word_usages",
                column: "WordSenseId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "word_usages");
        }
    }
}
