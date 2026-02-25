using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentInvoiceIdToOnlinePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StudentInvoiceId",
                table: "OnlinePayments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlinePayments_StudentInvoiceId",
                table: "OnlinePayments",
                column: "StudentInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_OnlinePayments_StudentInvoices_StudentInvoiceId",
                table: "OnlinePayments",
                column: "StudentInvoiceId",
                principalTable: "StudentInvoices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnlinePayments_StudentInvoices_StudentInvoiceId",
                table: "OnlinePayments");

            migrationBuilder.DropIndex(
                name: "IX_OnlinePayments_StudentInvoiceId",
                table: "OnlinePayments");

            migrationBuilder.DropColumn(
                name: "StudentInvoiceId",
                table: "OnlinePayments");
        }
    }
}
