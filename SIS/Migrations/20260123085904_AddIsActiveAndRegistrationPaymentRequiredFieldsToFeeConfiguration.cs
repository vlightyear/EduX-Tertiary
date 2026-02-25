using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveAndRegistrationPaymentRequiredFieldsToFeeConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "FeeConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RegistrationPaymentRequired",
                table: "FeeConfigurations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 75m);

            /*migrationBuilder.CreateIndex(
                name: "IX_OnlinePayments_StudentId",
                table: "OnlinePayments",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_OnlinePayments_Students_StudentId",
                table: "OnlinePayments",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);*/
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            /*migrationBuilder.DropForeignKey(
                name: "FK_OnlinePayments_Students_StudentId",
                table: "OnlinePayments");

            migrationBuilder.DropIndex(
                name: "IX_OnlinePayments_StudentId",
                table: "OnlinePayments");*/

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "FeeConfigurations");

            migrationBuilder.DropColumn(
                name: "RegistrationPaymentRequired",
                table: "FeeConfigurations");
        }
    }
}
