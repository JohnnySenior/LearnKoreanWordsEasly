using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backup_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FileName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Verified = table.Column<bool>(type: "boolean", nullable: false),
                    RestoredCounts = table.Column<string>(type: "jsonb", nullable: true),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SentToTelegramAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TelegramAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TelegramError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemindersEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WeekdayReminderTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BotBlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AiProvider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_usage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Feature = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EstimatedUsd = table.Column<decimal>(type: "numeric", nullable: true),
                    PriceAsOf = table.Column<DateOnly>(type: "date", nullable: true),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_usage_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "listening_sets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Items = table.Column<string>(type: "jsonb", nullable: false),
                    Answers = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listening_sets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_listening_sets_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reminder_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderMessageId = table.Column<long>(type: "bigint", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminder_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reminder_deliveries_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sentence_ladders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Items = table.Column<string>(type: "jsonb", nullable: false),
                    Answers = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sentence_ladders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sentence_ladders_users_OwnerId",
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
                name: "test_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Questions = table.Column<string>(type: "jsonb", nullable: false),
                    Answers = table.Column<string>(type: "jsonb", nullable: false),
                    Warning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RetryOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_test_runs_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trap_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LadderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Uzbek = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Wrong = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Right = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExplanationUz = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trap_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trap_notes_users_OwnerId",
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
                    TableNumber = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
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
                name: "sentence_checks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordSenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Target = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetMeaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Answer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sentence_checks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sentence_checks_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sentence_checks_word_senses_WordSenseId",
                        column: x => x.WordSenseId,
                        principalTable: "word_senses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "word_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordSenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    Phrases = table.Column<string>(type: "jsonb", nullable: false),
                    TrapUz = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Example = table.Column<string>(type: "jsonb", nullable: true),
                    Questions = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Warning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_usages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_usages_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_word_usages_word_senses_WordSenseId",
                        column: x => x.WordSenseId,
                        principalTable: "word_senses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "writing_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordSenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Answer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Expected = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Verdict = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AiChecked = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_writing_attempts_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_writing_attempts_word_senses_WordSenseId",
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
                name: "IX_ai_usage_OwnerId_OccurredAt",
                table: "ai_usage",
                columns: new[] { "OwnerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_backup_runs_StartedAt",
                table: "backup_runs",
                column: "StartedAt");

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
                name: "IX_listening_sets_OwnerId_CreatedAt",
                table: "listening_sets",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reminder_deliveries_OccurrenceKey",
                table: "reminder_deliveries",
                column: "OccurrenceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reminder_deliveries_UserId_ScheduledAt",
                table: "reminder_deliveries",
                columns: new[] { "UserId", "ScheduledAt" });

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
                name: "IX_sentence_checks_OwnerId_CreatedAt",
                table: "sentence_checks",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sentence_checks_WordSenseId",
                table: "sentence_checks",
                column: "WordSenseId");

            migrationBuilder.CreateIndex(
                name: "IX_sentence_ladders_OwnerId_CreatedAt",
                table: "sentence_ladders",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_study_sessions_OwnerId_Status",
                table: "study_sessions",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_test_runs_OwnerId_CreatedAt",
                table: "test_runs",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_trap_notes_OwnerId_CreatedAt",
                table: "trap_notes",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_users_TelegramUserId",
                table: "users",
                column: "TelegramUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_senses_OwnerId_TableNumber_Order",
                table: "word_senses",
                columns: new[] { "OwnerId", "TableNumber", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_usages_OwnerId",
                table: "word_usages",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_word_usages_WordSenseId",
                table: "word_usages",
                column: "WordSenseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_writing_attempts_OwnerId_CreatedAt",
                table: "writing_attempts",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_writing_attempts_WordSenseId",
                table: "writing_attempts",
                column: "WordSenseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage");

            migrationBuilder.DropTable(
                name: "backup_runs");

            migrationBuilder.DropTable(
                name: "listening_sets");

            migrationBuilder.DropTable(
                name: "reminder_deliveries");

            migrationBuilder.DropTable(
                name: "review_events");

            migrationBuilder.DropTable(
                name: "sentence_checks");

            migrationBuilder.DropTable(
                name: "sentence_ladders");

            migrationBuilder.DropTable(
                name: "study_sessions");

            migrationBuilder.DropTable(
                name: "test_runs");

            migrationBuilder.DropTable(
                name: "trap_notes");

            migrationBuilder.DropTable(
                name: "word_usages");

            migrationBuilder.DropTable(
                name: "writing_attempts");

            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropTable(
                name: "word_senses");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
