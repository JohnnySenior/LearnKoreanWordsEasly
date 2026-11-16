using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PronunciationNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PronunciationKo",
                table: "personal_words",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PronunciationNoteUz",
                table: "personal_words",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PronunciationReviewed",
                table: "personal_words",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PronunciationKo",
                table: "group_deck_words",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PronunciationNoteUz",
                table: "group_deck_words",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PronunciationReviewed",
                table: "group_deck_words",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "group_deck_words",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "PronunciationKo",
                table: "catalog_words",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PronunciationNoteUz",
                table: "catalog_words",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PronunciationReviewed",
                table: "catalog_words",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PronunciationKo",
                table: "personal_words");

            migrationBuilder.DropColumn(
                name: "PronunciationNoteUz",
                table: "personal_words");

            migrationBuilder.DropColumn(
                name: "PronunciationReviewed",
                table: "personal_words");

            migrationBuilder.DropColumn(
                name: "PronunciationKo",
                table: "group_deck_words");

            migrationBuilder.DropColumn(
                name: "PronunciationNoteUz",
                table: "group_deck_words");

            migrationBuilder.DropColumn(
                name: "PronunciationReviewed",
                table: "group_deck_words");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "group_deck_words");

            migrationBuilder.DropColumn(
                name: "PronunciationKo",
                table: "catalog_words");

            migrationBuilder.DropColumn(
                name: "PronunciationNoteUz",
                table: "catalog_words");

            migrationBuilder.DropColumn(
                name: "PronunciationReviewed",
                table: "catalog_words");
        }
    }
}
