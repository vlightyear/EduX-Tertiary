using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentsIncoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    InvoiceReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", maxLength: 20, nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentInvoices_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId");
                    table.ForeignKey(
                        name: "FK_StudentInvoices_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentInvoiceId = table.Column<int>(type: "int", nullable: false),
                    FeeTypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FeeConfigurationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentInvoiceItems_FeeConfigurations_FeeConfigurationId",
                        column: x => x.FeeConfigurationId,
                        principalTable: "FeeConfigurations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentInvoiceItems_StudentInvoices_StudentInvoiceId",
                        column: x => x.StudentInvoiceId,
                        principalTable: "StudentInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentInvoiceItems_FeeConfigurationId",
                table: "StudentInvoiceItems",
                column: "FeeConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentInvoiceItems_StudentInvoiceId",
                table: "StudentInvoiceItems",
                column: "StudentInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentInvoice_InvoiceReference",
                table: "StudentInvoices",
                column: "InvoiceReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentInvoice_Student_AcademicYear_Semester",
                table: "StudentInvoices",
                columns: new[] { "StudentId", "AcademicYearId", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentInvoices_AcademicYearId",
                table: "StudentInvoices",
                column: "AcademicYearId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentInvoiceItems");

            migrationBuilder.DropTable(
                name: "StudentInvoices");
        }
    }
}
