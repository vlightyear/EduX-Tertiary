using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkingDayConfigurationsUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "WorkingDayConfigurations");

            migrationBuilder.DropColumn(
                name: "ModeOfStudy",
                table: "WorkingDayConfigurations");

            migrationBuilder.AddColumn<int>(
                name: "AcademicYearId",
                table: "WorkingDayConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModeOfStudyId",
                table: "WorkingDayConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WorkingDayConfigurations_AcademicYearId",
                table: "WorkingDayConfigurations",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingDayConfigurations_ModeOfStudyId",
                table: "WorkingDayConfigurations",
                column: "ModeOfStudyId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkingDayConfigurations_AcademicYears_AcademicYearId",
                table: "WorkingDayConfigurations",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "YearId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkingDayConfigurations_ModesOfStudy_ModeOfStudyId",
                table: "WorkingDayConfigurations",
                column: "ModeOfStudyId",
                principalTable: "ModesOfStudy",
                principalColumn: "ModeId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkingDayConfigurations_AcademicYears_AcademicYearId",
                table: "WorkingDayConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkingDayConfigurations_ModesOfStudy_ModeOfStudyId",
                table: "WorkingDayConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_WorkingDayConfigurations_AcademicYearId",
                table: "WorkingDayConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_WorkingDayConfigurations_ModeOfStudyId",
                table: "WorkingDayConfigurations");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "WorkingDayConfigurations");

            migrationBuilder.DropColumn(
                name: "ModeOfStudyId",
                table: "WorkingDayConfigurations");

            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "WorkingDayConfigurations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModeOfStudy",
                table: "WorkingDayConfigurations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
