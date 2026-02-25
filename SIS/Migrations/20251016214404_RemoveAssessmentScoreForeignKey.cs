using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAssessmentScoreForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentAssessmentScores_StudentCourseResults_StudentId_CourseId_AcademicYearId",
                table: "StudentAssessmentScores");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_StudentCourseResults_StudentId_CourseId_AcademicYearId",
                table: "StudentCourseResults");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_StudentCourseResults_StudentId_CourseId_AcademicYearId",
                table: "StudentCourseResults",
                columns: new[] { "StudentId", "CourseId", "AcademicYearId" });

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAssessmentScores_StudentCourseResults_StudentId_CourseId_AcademicYearId",
                table: "StudentAssessmentScores",
                columns: new[] { "StudentId", "CourseId", "AcademicYearId" },
                principalTable: "StudentCourseResults",
                principalColumns: new[] { "StudentId", "CourseId", "AcademicYearId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
