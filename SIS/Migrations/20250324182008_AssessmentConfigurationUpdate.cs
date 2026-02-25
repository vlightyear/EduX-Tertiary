using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentConfigurationUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "AssessmentConfigurations");

            migrationBuilder.DropColumn(
                name: "ModeOfStudy",
                table: "AssessmentConfigurations");

            migrationBuilder.AddColumn<int>(
                name: "AcademicYearId",
                table: "AssessmentConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModeOfStudyId",
                table: "AssessmentConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentConfigurations_AcademicYearId",
                table: "AssessmentConfigurations",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentConfigurations_ModeOfStudyId",
                table: "AssessmentConfigurations",
                column: "ModeOfStudyId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssessmentConfigurations_AcademicYears_AcademicYearId",
                table: "AssessmentConfigurations",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "YearId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AssessmentConfigurations_ModesOfStudy_ModeOfStudyId",
                table: "AssessmentConfigurations",
                column: "ModeOfStudyId",
                principalTable: "ModesOfStudy",
                principalColumn: "ModeId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssessmentConfigurations_AcademicYears_AcademicYearId",
                table: "AssessmentConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_AssessmentConfigurations_ModesOfStudy_ModeOfStudyId",
                table: "AssessmentConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_AssessmentConfigurations_AcademicYearId",
                table: "AssessmentConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_AssessmentConfigurations_ModeOfStudyId",
                table: "AssessmentConfigurations");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AssessmentConfigurations");

            migrationBuilder.DropColumn(
                name: "ModeOfStudyId",
                table: "AssessmentConfigurations");

            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "AssessmentConfigurations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModeOfStudy",
                table: "AssessmentConfigurations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
