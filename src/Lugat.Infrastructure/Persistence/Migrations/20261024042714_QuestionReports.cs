using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "question_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Sentence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Translation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Options = table.Column<string>(type: "jsonb", nullable: false),
                    Correct = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Given = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LearningItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_reports_users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_question_reports_ReporterId_TestRunId_QuestionIndex",
                table: "question_reports",
                columns: new[] { "ReporterId", "TestRunId", "QuestionIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_reports_ResolvedAt",
                table: "question_reports",
                column: "ResolvedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_reports");
        }
    }
}
