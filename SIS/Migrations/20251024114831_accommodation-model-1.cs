using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class accommodationmodel1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AccountCode",
                table: "AccomodationConfiguration",
                newName: "PaymentAccountCode");

            migrationBuilder.AlterColumn<string>(
                name: "LocationToTakeAccommodationPaymentReceipt",
                table: "AccomodationConfiguration",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceAccountCode",
                table: "AccomodationConfiguration",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceAccountCode",
                table: "AccomodationConfiguration");

            migrationBuilder.RenameColumn(
                name: "PaymentAccountCode",
                table: "AccomodationConfiguration",
                newName: "AccountCode");

            migrationBuilder.AlterColumn<decimal>(
                name: "LocationToTakeAccommodationPaymentReceipt",
                table: "AccomodationConfiguration",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
