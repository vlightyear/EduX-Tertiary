using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkingDayConfigurationsUpdateNulls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkingDayConfigurations_AcademicYears_AcademicYearId",
                table: "WorkingDayConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkingDayConfigurations_ModesOfStudy_ModeOfStudyId",
                table: "WorkingDayConfigurations");

            migrationBuilder.AlterColumn<int>(
                name: "ModeOfStudyId",
                table: "WorkingDayConfigurations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "AcademicYearId",
                table: "WorkingDayConfigurations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkingDayConfigurations_AcademicYears_AcademicYearId",
                table: "WorkingDayConfigurations",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "YearId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkingDayConfigurations_ModesOfStudy_ModeOfStudyId",
                table: "WorkingDayConfigurations",
                column: "ModeOfStudyId",
                principalTable: "ModesOfStudy",
                principalColumn: "ModeId");
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

            migrationBuilder.AlterColumn<int>(
                name: "ModeOfStudyId",
                table: "WorkingDayConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AcademicYearId",
                table: "WorkingDayConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

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
    }
}
