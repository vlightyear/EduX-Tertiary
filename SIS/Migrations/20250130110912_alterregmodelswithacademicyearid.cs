using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class alterregmodelswithacademicyearid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AcademicYear",
                table: "StudentExaminableCourses",
                newName: "AcademicYearId");

            migrationBuilder.RenameColumn(
                name: "AcademicYear",
                table: "StudentCourseRegistrations",
                newName: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentExaminableCourses_AcademicYearId",
                table: "StudentExaminableCourses",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseRegistrations_AcademicYearId",
                table: "StudentCourseRegistrations",
                column: "AcademicYearId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentCourseRegistrations_AcademicYears_AcademicYearId",
                table: "StudentCourseRegistrations",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "YearId",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentExaminableCourses_AcademicYears_AcademicYearId",
                table: "StudentExaminableCourses",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "YearId",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentCourseRegistrations_AcademicYears_AcademicYearId",
                table: "StudentCourseRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentExaminableCourses_AcademicYears_AcademicYearId",
                table: "StudentExaminableCourses");

            migrationBuilder.DropIndex(
                name: "IX_StudentExaminableCourses_AcademicYearId",
                table: "StudentExaminableCourses");

            migrationBuilder.DropIndex(
                name: "IX_StudentCourseRegistrations_AcademicYearId",
                table: "StudentCourseRegistrations");

            migrationBuilder.RenameColumn(
                name: "AcademicYearId",
                table: "StudentExaminableCourses",
                newName: "AcademicYear");

            migrationBuilder.RenameColumn(
                name: "AcademicYearId",
                table: "StudentCourseRegistrations",
                newName: "AcademicYear");
        }
    }
}
