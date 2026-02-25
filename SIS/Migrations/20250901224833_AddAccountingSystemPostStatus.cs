using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingSystemPostStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountingSystemPostStatus",
                table: "StudentInvoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountingSystemPostStatus",
                table: "StudentInvoiceItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountingSystemPostStatus",
                table: "StudentInvoices");

            migrationBuilder.DropColumn(
                name: "AccountingSystemPostStatus",
                table: "StudentInvoiceItems");
        }
    }
}
