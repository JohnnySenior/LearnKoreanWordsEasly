using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ListeningSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_listening_sets_OwnerId_CreatedAt",
                table: "listening_sets",
                columns: new[] { "OwnerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listening_sets");
        }
    }
}
