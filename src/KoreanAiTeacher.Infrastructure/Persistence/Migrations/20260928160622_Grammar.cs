using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KoreanAiTeacher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Grammar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grammar_imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPage = table.Column<int>(type: "integer", nullable: false),
                    ToPage = table.Column<int>(type: "integer", nullable: false),
                    DonePage = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grammar_imports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grammar_imports_source_documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "source_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_grammar_imports_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grammar_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrammarImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    PdfPage = table.Column<int>(type: "integer", nullable: false),
                    OrderInDocument = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: true),
                    Section = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Form = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Similar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MeaningKo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MeaningUz = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ConnectionUz = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RestrictionUz = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Examples = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Flagged = table.Column<bool>(type: "boolean", nullable: false),
                    Warning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grammar_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grammar_items_grammar_imports_GrammarImportId",
                        column: x => x.GrammarImportId,
                        principalTable: "grammar_imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_grammar_items_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grammar_imports_OwnerId",
                table: "grammar_imports",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_grammar_imports_SourceDocumentId",
                table: "grammar_imports",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_grammar_imports_Status_CreatedAt",
                table: "grammar_imports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_grammar_items_GrammarImportId_PdfPage",
                table: "grammar_items",
                columns: new[] { "GrammarImportId", "PdfPage" });

            migrationBuilder.CreateIndex(
                name: "IX_grammar_items_OwnerId_Status",
                table: "grammar_items",
                columns: new[] { "OwnerId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grammar_items");

            migrationBuilder.DropTable(
                name: "grammar_imports");
        }
    }
}
