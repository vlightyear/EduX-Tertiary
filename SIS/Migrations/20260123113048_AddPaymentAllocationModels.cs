using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAllocationModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OnlinePaymentId = table.Column<int>(type: "int", nullable: false),
                    StudentInvoiceId = table.Column<int>(type: "int", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InvoiceBalanceBeforeAllocation = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InvoiceBalanceAfterAllocation = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocationSequence = table.Column<int>(type: "int", nullable: false),
                    AllocatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AllocatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_OnlinePayments_OnlinePaymentId",
                        column: x => x.OnlinePaymentId,
                        principalTable: "OnlinePayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_StudentInvoices_StudentInvoiceId",
                        column: x => x.StudentInvoiceId,
                        principalTable: "StudentInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_OnlinePaymentId_AllocationSequence",
                table: "PaymentAllocations",
                columns: new[] { "OnlinePaymentId", "AllocationSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_StudentInvoiceId",
                table: "PaymentAllocations",
                column: "StudentInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAllocations");
        }
    }
}
