using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrammarSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grammar_sets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grammar_sets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grammar_sets_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grammar_set_items",
                columns: table => new
                {
                    GrammarSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrammarItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grammar_set_items", x => new { x.GrammarSetId, x.GrammarItemId });
                    table.ForeignKey(
                        name: "FK_grammar_set_items_grammar_items_GrammarItemId",
                        column: x => x.GrammarItemId,
                        principalTable: "grammar_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_grammar_set_items_grammar_sets_GrammarSetId",
                        column: x => x.GrammarSetId,
                        principalTable: "grammar_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grammar_set_items_GrammarItemId",
                table: "grammar_set_items",
                column: "GrammarItemId");

            migrationBuilder.CreateIndex(
                name: "IX_grammar_set_items_OwnerId_GrammarItemId",
                table: "grammar_set_items",
                columns: new[] { "OwnerId", "GrammarItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grammar_sets_OwnerId_Number",
                table: "grammar_sets",
                columns: new[] { "OwnerId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grammar_set_items");

            migrationBuilder.DropTable(
                name: "grammar_sets");
        }
    }
}
