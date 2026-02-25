using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditColumnsToInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BlackListedFromAccommodationReason",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "StudentInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "StudentInvoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "StudentInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "StudentInvoices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "StudentInvoices");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "StudentInvoices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "StudentInvoices");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "StudentInvoices");

            migrationBuilder.AlterColumn<string>(
                name: "BlackListedFromAccommodationReason",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
