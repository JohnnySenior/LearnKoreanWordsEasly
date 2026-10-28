using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersonalWords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "word_decks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_decks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_decks_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personal_words",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeckId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CatalogWordId = table.Column<Guid>(type: "uuid", nullable: true),
                    Romanization = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExampleKo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExampleUz = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TrapUz = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IntroduceOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_words", x => x.Id);
                    table.ForeignKey(
                        name: "FK_personal_words_learning_items_LearningItemId",
                        column: x => x.LearningItemId,
                        principalTable: "learning_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personal_words_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_personal_words_word_decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "word_decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personal_words_DeckId",
                table: "personal_words",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_personal_words_LearningItemId",
                table: "personal_words",
                column: "LearningItemId");

            migrationBuilder.CreateIndex(
                name: "IX_personal_words_OwnerId_IntroduceOn",
                table: "personal_words",
                columns: new[] { "OwnerId", "IntroduceOn" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_words_OwnerId_LearningItemId",
                table: "personal_words",
                columns: new[] { "OwnerId", "LearningItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_decks_OwnerId_Name",
                table: "word_decks",
                columns: new[] { "OwnerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personal_words");

            migrationBuilder.DropTable(
                name: "word_decks");
        }
    }
}
