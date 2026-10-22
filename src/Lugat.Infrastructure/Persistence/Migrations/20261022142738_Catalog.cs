using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Catalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WordCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_words",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Word = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Homonym = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Pos = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Gloss = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Level = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    FreqRank = table.Column<int>(type: "integer", nullable: false),
                    IsStudyable = table.Column<bool>(type: "boolean", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderInUnit = table.Column<int>(type: "integer", nullable: true),
                    TranslationUz = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Romanization = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExampleKo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExampleUz = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TrapUz = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Theme = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AiProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AiModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DraftedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_words", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_words_units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_words_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "catalog_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogWordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    Questions = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_usages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_usages_catalog_words_CatalogWordId",
                        column: x => x.CatalogWordId,
                        principalTable: "catalog_words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_usages_CatalogWordId",
                table: "catalog_usages",
                column: "CatalogWordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_words_Level_FreqRank",
                table: "catalog_words",
                columns: new[] { "Level", "FreqRank" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_words_ReviewedByUserId",
                table: "catalog_words",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_words_SourceKey",
                table: "catalog_words",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_words_UnitId_OrderInUnit",
                table: "catalog_words",
                columns: new[] { "UnitId", "OrderInUnit" });

            migrationBuilder.CreateIndex(
                name: "IX_units_Level_Number",
                table: "units",
                columns: new[] { "Level", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_usages");

            migrationBuilder.DropTable(
                name: "catalog_words");

            migrationBuilder.DropTable(
                name: "units");
        }
    }
}
