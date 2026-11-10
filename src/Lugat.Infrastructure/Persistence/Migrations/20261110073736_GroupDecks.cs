using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupDecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FromGroupDeck",
                table: "personal_words",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupDeckId",
                table: "personal_words",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupDeckWordId",
                table: "personal_words",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "group_decks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_decks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_decks_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_decks_users_TeacherUserId",
                        column: x => x.TeacherUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_deck_words",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupDeckId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Korean = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Uzbek = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CatalogWordId = table.Column<Guid>(type: "uuid", nullable: true),
                    SharedItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Romanization = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExampleKo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExampleUz = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TrapUz = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TrapCue = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TrapWrong = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    TrapRight = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_deck_words", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_deck_words_catalog_words_CatalogWordId",
                        column: x => x.CatalogWordId,
                        principalTable: "catalog_words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_group_deck_words_group_decks_GroupDeckId",
                        column: x => x.GroupDeckId,
                        principalTable: "group_decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_deck_words_learning_items_SharedItemId",
                        column: x => x.SharedItemId,
                        principalTable: "learning_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personal_words_GroupDeckId_OwnerId",
                table: "personal_words",
                columns: new[] { "GroupDeckId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_words_GroupDeckWordId",
                table: "personal_words",
                column: "GroupDeckWordId");

            migrationBuilder.CreateIndex(
                name: "IX_group_deck_words_CatalogWordId",
                table: "group_deck_words",
                column: "CatalogWordId");

            migrationBuilder.CreateIndex(
                name: "IX_group_deck_words_GroupDeckId_Order",
                table: "group_deck_words",
                columns: new[] { "GroupDeckId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_group_deck_words_SharedItemId",
                table: "group_deck_words",
                column: "SharedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_group_decks_GroupId",
                table: "group_decks",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_group_decks_TeacherUserId",
                table: "group_decks",
                column: "TeacherUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_personal_words_group_deck_words_GroupDeckWordId",
                table: "personal_words",
                column: "GroupDeckWordId",
                principalTable: "group_deck_words",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_personal_words_group_decks_GroupDeckId",
                table: "personal_words",
                column: "GroupDeckId",
                principalTable: "group_decks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_personal_words_group_deck_words_GroupDeckWordId",
                table: "personal_words");

            migrationBuilder.DropForeignKey(
                name: "FK_personal_words_group_decks_GroupDeckId",
                table: "personal_words");

            migrationBuilder.DropTable(
                name: "group_deck_words");

            migrationBuilder.DropTable(
                name: "group_decks");

            migrationBuilder.DropIndex(
                name: "IX_personal_words_GroupDeckId_OwnerId",
                table: "personal_words");

            migrationBuilder.DropIndex(
                name: "IX_personal_words_GroupDeckWordId",
                table: "personal_words");

            migrationBuilder.DropColumn(
                name: "FromGroupDeck",
                table: "personal_words");

            migrationBuilder.DropColumn(
                name: "GroupDeckId",
                table: "personal_words");

            migrationBuilder.DropColumn(
                name: "GroupDeckWordId",
                table: "personal_words");
        }
    }
}
