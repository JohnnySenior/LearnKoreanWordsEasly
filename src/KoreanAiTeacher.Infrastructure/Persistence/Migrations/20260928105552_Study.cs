using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KoreanAiTeacher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Study : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lesson_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tables = table.Column<int[]>(type: "integer[]", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lesson_assignments_source_documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "source_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lesson_assignments_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Minutes = table.Column<int>(type: "integer", nullable: true),
                    Items = table.Column<string>(type: "jsonb", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_study_sessions_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "word_senses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableNumber = table.Column<int>(type: "integer", nullable: false),
                    OrderInDocument = table.Column<int>(type: "integer", nullable: false),
                    Korean = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KoreanNote = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Uzbek = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_senses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_senses_source_entries_SourceEntryId",
                        column: x => x.SourceEntryId,
                        principalTable: "source_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_word_senses_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordSenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Phase = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Interval = table.Column<TimeSpan>(type: "interval", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Lapses = table.Column<int>(type: "integer", nullable: false),
                    LastLapseAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IndependentSuccessDays = table.Column<int>(type: "integer", nullable: false),
                    LastIndependentSuccessDay = table.Column<DateOnly>(type: "date", nullable: true),
                    AlgorithmVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cards_word_senses_WordSenseId",
                        column: x => x.WordSenseId,
                        principalTable: "word_senses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionPosition = table.Column<int>(type: "integer", nullable: true),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordSenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Answer = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Assisted = table.Column<bool>(type: "boolean", nullable: false),
                    WasDue = table.Column<bool>(type: "boolean", nullable: false),
                    CountedAsIndependentSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ResponseMs = table.Column<int>(type: "integer", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PreviousState = table.Column<string>(type: "jsonb", nullable: false),
                    NextState = table.Column<string>(type: "jsonb", nullable: false),
                    ReinsertedAt = table.Column<int>(type: "integer", nullable: true),
                    UndoneAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_events_cards_CardId",
                        column: x => x.CardId,
                        principalTable: "cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cards_OwnerId_DueAt",
                table: "cards",
                columns: new[] { "OwnerId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cards_WordSenseId_Direction",
                table: "cards",
                columns: new[] { "WordSenseId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_assignments_OwnerId_Status_DueAt",
                table: "lesson_assignments",
                columns: new[] { "OwnerId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_assignments_SourceDocumentId",
                table: "lesson_assignments",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_review_events_CardId",
                table: "review_events",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_review_events_OwnerId_OccurredAt",
                table: "review_events",
                columns: new[] { "OwnerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_review_events_SessionId",
                table: "review_events",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_study_sessions_OwnerId_Status",
                table: "study_sessions",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_word_senses_OwnerId_SourceDocumentId_TableNumber",
                table: "word_senses",
                columns: new[] { "OwnerId", "SourceDocumentId", "TableNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_word_senses_SourceEntryId",
                table: "word_senses",
                column: "SourceEntryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_assignments");

            migrationBuilder.DropTable(
                name: "review_events");

            migrationBuilder.DropTable(
                name: "study_sessions");

            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropTable(
                name: "word_senses");
        }
    }
}
