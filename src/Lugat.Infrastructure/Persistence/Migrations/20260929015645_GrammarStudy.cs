using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrammarStudy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GrammarItemId",
                table: "sentence_checks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Guide",
                table: "grammar_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sentence_checks_GrammarItemId",
                table: "sentence_checks",
                column: "GrammarItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_sentence_checks_grammar_items_GrammarItemId",
                table: "sentence_checks",
                column: "GrammarItemId",
                principalTable: "grammar_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sentence_checks_grammar_items_GrammarItemId",
                table: "sentence_checks");

            migrationBuilder.DropIndex(
                name: "IX_sentence_checks_GrammarItemId",
                table: "sentence_checks");

            migrationBuilder.DropColumn(
                name: "GrammarItemId",
                table: "sentence_checks");

            migrationBuilder.DropColumn(
                name: "Guide",
                table: "grammar_items");
        }
    }
}
