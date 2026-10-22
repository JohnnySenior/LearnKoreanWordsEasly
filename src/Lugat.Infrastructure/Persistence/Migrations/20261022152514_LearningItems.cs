using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LearningItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cards_word_senses_WordSenseId",
                table: "cards");

            migrationBuilder.DropForeignKey(
                name: "FK_sentence_checks_word_senses_WordSenseId",
                table: "sentence_checks");

            migrationBuilder.DropForeignKey(
                name: "FK_word_usages_word_senses_WordSenseId",
                table: "word_usages");

            migrationBuilder.DropForeignKey(
                name: "FK_writing_attempts_word_senses_WordSenseId",
                table: "writing_attempts");

            // The interim per-learner words (L-04) never reached production. Their practice data points at
            // word_senses and cannot follow to the shared learning_items, so it goes with them (L-22).
            migrationBuilder.Sql("""
                DELETE FROM review_events; DELETE FROM study_sessions; DELETE FROM word_usages;
                DELETE FROM writing_attempts; DELETE FROM sentence_checks; DELETE FROM cards;
                """);

            migrationBuilder.DropTable(
                name: "word_senses");

            migrationBuilder.DropIndex(
                name: "IX_word_usages_WordSenseId",
                table: "word_usages");

            migrationBuilder.DropIndex(
                name: "IX_cards_WordSenseId_Direction",
                table: "cards");

            migrationBuilder.RenameColumn(
                name: "WordSenseId",
                table: "writing_attempts",
                newName: "LearningItemId");

            migrationBuilder.RenameIndex(
                name: "IX_writing_attempts_WordSenseId",
                table: "writing_attempts",
                newName: "IX_writing_attempts_LearningItemId");

            migrationBuilder.RenameColumn(
                name: "WordSenseId",
                table: "word_usages",
                newName: "LearningItemId");

            migrationBuilder.RenameColumn(
                name: "WordSenseId",
                table: "sentence_checks",
                newName: "LearningItemId");

            migrationBuilder.RenameIndex(
                name: "IX_sentence_checks_WordSenseId",
                table: "sentence_checks",
                newName: "IX_sentence_checks_LearningItemId");

            migrationBuilder.RenameColumn(
                name: "WordSenseId",
                table: "review_events",
                newName: "LearningItemId");

            migrationBuilder.RenameColumn(
                name: "WordSenseId",
                table: "cards",
                newName: "LearningItemId");

            migrationBuilder.CreateTable(
                name: "learning_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CatalogWordId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Korean = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KoreanNote = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Uzbek = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_items", x => x.Id);
                    table.CheckConstraint("CK_learning_items_kind", "(\"Kind\" = 'Catalog' AND \"CatalogWordId\" IS NOT NULL AND \"OwnerId\" IS NULL) OR (\"Kind\" = 'Personal' AND \"CatalogWordId\" IS NULL AND \"OwnerId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_learning_items_catalog_words_CatalogWordId",
                        column: x => x.CatalogWordId,
                        principalTable: "catalog_words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_learning_items_units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_learning_items_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_unit_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PassedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PassedBy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    BestScorePercent = table.Column<int>(type: "integer", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_unit_progress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_unit_progress_units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_unit_progress_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_word_usages_LearningItemId",
                table: "word_usages",
                column: "LearningItemId");

            migrationBuilder.CreateIndex(
                name: "IX_word_usages_OwnerId_LearningItemId",
                table: "word_usages",
                columns: new[] { "OwnerId", "LearningItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cards_LearningItemId",
                table: "cards",
                column: "LearningItemId");

            migrationBuilder.CreateIndex(
                name: "IX_cards_OwnerId_LearningItemId_Direction",
                table: "cards",
                columns: new[] { "OwnerId", "LearningItemId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_learning_items_CatalogWordId",
                table: "learning_items",
                column: "CatalogWordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_learning_items_OwnerId",
                table: "learning_items",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_learning_items_UnitId_Order",
                table: "learning_items",
                columns: new[] { "UnitId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_user_unit_progress_UnitId",
                table: "user_unit_progress",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_user_unit_progress_UserId_UnitId",
                table: "user_unit_progress",
                columns: new[] { "UserId", "UnitId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_cards_learning_items_LearningItemId",
                table: "cards",
                column: "LearningItemId",
                principalTable: "learning_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sentence_checks_learning_items_LearningItemId",
                table: "sentence_checks",
                column: "LearningItemId",
                principalTable: "learning_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_word_usages_learning_items_LearningItemId",
                table: "word_usages",
                column: "LearningItemId",
                principalTable: "learning_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_writing_attempts_learning_items_LearningItemId",
                table: "writing_attempts",
                column: "LearningItemId",
                principalTable: "learning_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cards_learning_items_LearningItemId",
                table: "cards");

            migrationBuilder.DropForeignKey(
                name: "FK_sentence_checks_learning_items_LearningItemId",
                table: "sentence_checks");

            migrationBuilder.DropForeignKey(
                name: "FK_word_usages_learning_items_LearningItemId",
                table: "word_usages");

            migrationBuilder.DropForeignKey(
                name: "FK_writing_attempts_learning_items_LearningItemId",
                table: "writing_attempts");

            migrationBuilder.DropTable(
                name: "learning_items");

            migrationBuilder.DropTable(
                name: "user_unit_progress");

            migrationBuilder.DropIndex(
                name: "IX_word_usages_LearningItemId",
                table: "word_usages");

            migrationBuilder.DropIndex(
                name: "IX_word_usages_OwnerId_LearningItemId",
                table: "word_usages");

            migrationBuilder.DropIndex(
                name: "IX_cards_LearningItemId",
                table: "cards");

            migrationBuilder.DropIndex(
                name: "IX_cards_OwnerId_LearningItemId_Direction",
                table: "cards");

            migrationBuilder.RenameColumn(
                name: "LearningItemId",
                table: "writing_attempts",
                newName: "WordSenseId");

            migrationBuilder.RenameIndex(
                name: "IX_writing_attempts_LearningItemId",
                table: "writing_attempts",
                newName: "IX_writing_attempts_WordSenseId");

            migrationBuilder.RenameColumn(
                name: "LearningItemId",
                table: "word_usages",
                newName: "WordSenseId");

            migrationBuilder.RenameColumn(
                name: "LearningItemId",
                table: "sentence_checks",
                newName: "WordSenseId");

            migrationBuilder.RenameIndex(
                name: "IX_sentence_checks_LearningItemId",
                table: "sentence_checks",
                newName: "IX_sentence_checks_WordSenseId");

            migrationBuilder.RenameColumn(
                name: "LearningItemId",
                table: "review_events",
                newName: "WordSenseId");

            migrationBuilder.RenameColumn(
                name: "LearningItemId",
                table: "cards",
                newName: "WordSenseId");

            migrationBuilder.CreateTable(
                name: "word_senses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Korean = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KoreanNote = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableNumber = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Uzbek = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_senses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_senses_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_word_usages_WordSenseId",
                table: "word_usages",
                column: "WordSenseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cards_WordSenseId_Direction",
                table: "cards",
                columns: new[] { "WordSenseId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_senses_OwnerId_TableNumber_Order",
                table: "word_senses",
                columns: new[] { "OwnerId", "TableNumber", "Order" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_cards_word_senses_WordSenseId",
                table: "cards",
                column: "WordSenseId",
                principalTable: "word_senses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sentence_checks_word_senses_WordSenseId",
                table: "sentence_checks",
                column: "WordSenseId",
                principalTable: "word_senses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_word_usages_word_senses_WordSenseId",
                table: "word_usages",
                column: "WordSenseId",
                principalTable: "word_senses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_writing_attempts_word_senses_WordSenseId",
                table: "writing_attempts",
                column: "WordSenseId",
                principalTable: "word_senses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
