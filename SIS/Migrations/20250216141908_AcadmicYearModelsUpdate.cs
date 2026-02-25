using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AcadmicYearModelsUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYears_WorkingDayConfigurations_WorkingDaysConfigId",
                table: "AcademicYears");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_WorkingDaysConfigId",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "DefaultTimeSlotDuration",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "WorkingDaysConfigId",
                table: "AcademicYears");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingDayConfigurations_AcademicYearId",
                table: "WorkingDayConfigurations",
                column: "AcademicYearId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkingDayConfigurations_AcademicYears_AcademicYearId",
                table: "WorkingDayConfigurations",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "YearId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkingDayConfigurations_AcademicYears_AcademicYearId",
                table: "WorkingDayConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_WorkingDayConfigurations_AcademicYearId",
                table: "WorkingDayConfigurations");

            migrationBuilder.AddColumn<int>(
                name: "DefaultTimeSlotDuration",
                table: "AcademicYears",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AcademicYears",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkingDaysConfigId",
                table: "AcademicYears",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_WorkingDaysConfigId",
                table: "AcademicYears",
                column: "WorkingDaysConfigId",
                unique: true,
                filter: "[WorkingDaysConfigId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicYears_WorkingDayConfigurations_WorkingDaysConfigId",
                table: "AcademicYears",
                column: "WorkingDaysConfigId",
                principalTable: "WorkingDayConfigurations",
                principalColumn: "Id");
        }
    }
}
