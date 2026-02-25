using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddManualPaymentFieldsToOnlinePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostedBy",
                table: "OnlinePayments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofOfPaymentPath",
                table: "OnlinePayments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SageSystemPostStatus",
                table: "OnlinePayments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PostedBy",
                table: "OnlinePayments");

            migrationBuilder.DropColumn(
                name: "ProofOfPaymentPath",
                table: "OnlinePayments");

            migrationBuilder.DropColumn(
                name: "SageSystemPostStatus",
                table: "OnlinePayments");
        }
    }
}
