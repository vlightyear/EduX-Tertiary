using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class updatedaccommodationmodule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationPeriods_AcademicYears_AcademicYearId",
                table: "AccommodationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationPeriods_AcademicYearId",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AccommodationPeriods");

            migrationBuilder.AddColumn<string>(
                name: "TypeOfPayment",
                table: "AccommodationPeriods",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                defaultValue: "Semester");

            migrationBuilder.AddColumn<decimal>(
                name: "TypeOfPaymentAmount",
                table: "AccommodationPeriods",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfDays",
                table: "AccommodationApplications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_Status",
                table: "AccommodationPeriods",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_Status_Dates",
                table: "AccommodationPeriods",
                columns: new[] { "Status", "ApplicationStartDate", "ApplicationEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_TypeOfPayment",
                table: "AccommodationPeriods",
                column: "TypeOfPayment");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationApplications_Student_Status",
                table: "AccommodationApplications",
                columns: new[] { "StudentId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccommodationPeriods_Status",
                table: "AccommodationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationPeriods_Status_Dates",
                table: "AccommodationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationPeriods_TypeOfPayment",
                table: "AccommodationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationApplications_Student_Status",
                table: "AccommodationApplications");

            migrationBuilder.DropColumn(
                name: "TypeOfPayment",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "TypeOfPaymentAmount",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "NumberOfDays",
                table: "AccommodationApplications");

            migrationBuilder.AddColumn<int>(
                name: "AcademicYearId",
                table: "AccommodationPeriods",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_AcademicYearId",
                table: "AccommodationPeriods",
                column: "AcademicYearId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationPeriods_AcademicYears_AcademicYearId",
                table: "AccommodationPeriods",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "YearId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
