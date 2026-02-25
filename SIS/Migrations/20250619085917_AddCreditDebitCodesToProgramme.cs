using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditDebitCodesToProgramme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreditNCode",
                table: "Programmes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "10000001");

            migrationBuilder.AddColumn<string>(
                name: "DebitNCode",
                table: "Programmes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "60000001");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditNCode",
                table: "Programmes");

            migrationBuilder.DropColumn(
                name: "DebitNCode",
                table: "Programmes");
        }
    }
}
