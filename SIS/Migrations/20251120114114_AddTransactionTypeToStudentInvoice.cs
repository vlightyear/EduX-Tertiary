using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionTypeToStudentInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
      

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "StudentInvoices",
                type: "nvarchar(max)",
                nullable: true);

           
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
      

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "StudentInvoices");

         
        }
    }
}
