using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KoreanAiTeacher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrammarNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SeenAt",
                table: "grammar_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "grammar_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrammarItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Question = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AnswerUz = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Examples = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grammar_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grammar_notes_grammar_items_GrammarItemId",
                        column: x => x.GrammarItemId,
                        principalTable: "grammar_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_grammar_notes_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grammar_notes_GrammarItemId_CreatedAt",
                table: "grammar_notes",
                columns: new[] { "GrammarItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_grammar_notes_OwnerId",
                table: "grammar_notes",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grammar_notes");

            migrationBuilder.DropColumn(
                name: "SeenAt",
                table: "grammar_items");
        }
    }
}
