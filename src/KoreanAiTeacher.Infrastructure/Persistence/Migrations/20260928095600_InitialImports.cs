using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KoreanAiTeacher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "source_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_source_documents_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ParserVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_jobs_source_documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "source_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_import_jobs_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "source_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PdfPage = table.Column<int>(type: "integer", nullable: false),
                    PrintedPage = table.Column<int>(type: "integer", nullable: true),
                    TableNumber = table.Column<int>(type: "integer", nullable: false),
                    OrderInTable = table.Column<int>(type: "integer", nullable: false),
                    OrderInDocument = table.Column<int>(type: "integer", nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    OriginalKorean = table.Column<string>(type: "text", nullable: false),
                    OriginalKoreanNote = table.Column<string>(type: "text", nullable: true),
                    OriginalUzbek = table.Column<string>(type: "text", nullable: false),
                    Korean = table.Column<string>(type: "text", nullable: false),
                    KoreanNote = table.Column<string>(type: "text", nullable: true),
                    Uzbek = table.Column<string>(type: "text", nullable: false),
                    Warnings = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_source_entries_import_jobs_ImportJobId",
                        column: x => x.ImportJobId,
                        principalTable: "import_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_source_entries_source_documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "source_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_jobs_OwnerId_CreatedAt",
                table: "import_jobs",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_import_jobs_SourceDocumentId",
                table: "import_jobs",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_import_jobs_Status",
                table: "import_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_source_documents_OwnerId_Kind_ContentSha256",
                table: "source_documents",
                columns: new[] { "OwnerId", "Kind", "ContentSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_entries_ImportJobId_TableNumber_OrderInTable",
                table: "source_entries",
                columns: new[] { "ImportJobId", "TableNumber", "OrderInTable" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_entries_OwnerId",
                table: "source_entries",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_source_entries_SourceDocumentId",
                table: "source_entries",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_users_TelegramUserId",
                table: "users",
                column: "TelegramUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "source_entries");

            migrationBuilder.DropTable(
                name: "import_jobs");

            migrationBuilder.DropTable(
                name: "source_documents");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
