using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KoreanAiTeacher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BookTestId",
                table: "test_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitMinutes",
                table: "test_runs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "book_tests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FromPage = table.Column<int>(type: "integer", nullable: false),
                    ToPage = table.Column<int>(type: "integer", nullable: false),
                    AnswerFromPage = table.Column<int>(type: "integer", nullable: true),
                    AnswerToPage = table.Column<int>(type: "integer", nullable: true),
                    DonePage = table.Column<int>(type: "integer", nullable: true),
                    AnswerDonePage = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_tests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_book_tests_source_documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "source_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_book_tests_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "book_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookTestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    PdfPage = table.Column<int>(type: "integer", nullable: false),
                    Instruction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Passage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Stem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Options = table.Column<string>(type: "jsonb", nullable: false),
                    Figure = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AiAnswer = table.Column<int>(type: "integer", nullable: false),
                    OfficialAnswer = table.Column<int>(type: "integer", nullable: true),
                    HintUz = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    ExplanationUz = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    Flagged = table.Column<bool>(type: "boolean", nullable: false),
                    Warning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Excluded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_book_questions_book_tests_BookTestId",
                        column: x => x.BookTestId,
                        principalTable: "book_tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_questions_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_book_questions_BookTestId_Number",
                table: "book_questions",
                columns: new[] { "BookTestId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_book_questions_OwnerId",
                table: "book_questions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_book_tests_OwnerId_CreatedAt",
                table: "book_tests",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_book_tests_SourceDocumentId",
                table: "book_tests",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_book_tests_Status",
                table: "book_tests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_questions");

            migrationBuilder.DropTable(
                name: "book_tests");

            migrationBuilder.DropColumn(
                name: "BookTestId",
                table: "test_runs");

            migrationBuilder.DropColumn(
                name: "TimeLimitMinutes",
                table: "test_runs");
        }
    }
}
