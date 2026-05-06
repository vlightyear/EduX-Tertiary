using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicPeriodToProgressionRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Semester",
                table: "ProgressionRules",
                newName: "AcademicPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressionRules_AcademicPeriodId",
                table: "ProgressionRules",
                column: "AcademicPeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressionRules_AcademicPeriods_AcademicPeriodId",
                table: "ProgressionRules",
                column: "AcademicPeriodId",
                principalTable: "AcademicPeriods",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgressionRules_AcademicPeriods_AcademicPeriodId",
                table: "ProgressionRules");

            migrationBuilder.DropIndex(
                name: "IX_ProgressionRules_AcademicPeriodId",
                table: "ProgressionRules");

            migrationBuilder.RenameColumn(
                name: "AcademicPeriodId",
                table: "ProgressionRules",
                newName: "Semester");
        }
    }
}
