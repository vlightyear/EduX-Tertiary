using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class addededcreditdebitcodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PaymentAccountCode",
                table: "AccomodationConfiguration",
                newName: "DebitCode");

            migrationBuilder.RenameColumn(
                name: "InvoiceAccountCode",
                table: "AccomodationConfiguration",
                newName: "CreditCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DebitCode",
                table: "AccomodationConfiguration",
                newName: "PaymentAccountCode");

            migrationBuilder.RenameColumn(
                name: "CreditCode",
                table: "AccomodationConfiguration",
                newName: "InvoiceAccountCode");
        }
    }
}
