using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lugat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_commission_payouts_TeacherUserId_Month",
                table: "commission_payouts");

            migrationBuilder.RenameColumn(
                name: "ExpectedKrw",
                table: "payments",
                newName: "ExpectedAmount");

            migrationBuilder.RenameColumn(
                name: "CommissionKrw",
                table: "payments",
                newName: "Commission");

            migrationBuilder.RenameColumn(
                name: "AmountKrw",
                table: "payments",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "AmountKrw",
                table: "commission_payouts",
                newName: "Amount");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "payments",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Krw"); // every payment so far was in won

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "commission_payouts",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Krw"); // every payment so far was in won

            migrationBuilder.CreateIndex(
                name: "IX_commission_payouts_TeacherUserId_Month_Currency",
                table: "commission_payouts",
                columns: new[] { "TeacherUserId", "Month", "Currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_commission_payouts_TeacherUserId_Month_Currency",
                table: "commission_payouts");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "commission_payouts");

            migrationBuilder.RenameColumn(
                name: "ExpectedAmount",
                table: "payments",
                newName: "ExpectedKrw");

            migrationBuilder.RenameColumn(
                name: "Commission",
                table: "payments",
                newName: "CommissionKrw");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "payments",
                newName: "AmountKrw");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "commission_payouts",
                newName: "AmountKrw");

            migrationBuilder.CreateIndex(
                name: "IX_commission_payouts_TeacherUserId_Month",
                table: "commission_payouts",
                columns: new[] { "TeacherUserId", "Month" },
                unique: true);
        }
    }
}
