using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class addedindices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResultSubmissionBatches_CourseId",
                table: "ResultSubmissionBatches");

            migrationBuilder.RenameIndex(
                name: "IX_StudentAssessmentScores_rsbId",
                table: "StudentAssessmentScores",
                newName: "IX_StudentAssessmentScores_RsbId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_Programme_CurrentYear",
                table: "Students",
                columns: new[] { "ProgrammeId", "StudentCurrentYear" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_Programme_Mode_Year",
                table: "Students",
                columns: new[] { "ProgrammeId", "ModeOfStudyId", "StudentCurrentYear" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentExaminableCourses_Course_Year_Semester",
                table: "StudentExaminableCourses",
                columns: new[] { "CourseId", "AcademicYearId", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentExaminableCourses_Full_Composite",
                table: "StudentExaminableCourses",
                columns: new[] { "StudentId", "CourseId", "AcademicYearId", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentExaminableCourses_Status",
                table: "StudentExaminableCourses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentExaminableCourses_Student_Year_Semester",
                table: "StudentExaminableCourses",
                columns: new[] { "StudentId", "AcademicYearId", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_Active_Course_Year",
                table: "StudentAssessmentScores",
                columns: new[] { "IsActive", "CourseId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_Course_Year_Semester_Student",
                table: "StudentAssessmentScores",
                columns: new[] { "CourseId", "AcademicYearId", "Semester", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Schools_Dean_AssistantDean",
                table: "Schools",
                columns: new[] { "DeanId", "AssistantDeanId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_ApprovalStatus",
                table: "ResultSubmissionBatches",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_Course_Status",
                table: "ResultSubmissionBatches",
                columns: new[] { "CourseId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_Course_Year_Semester",
                table: "ResultSubmissionBatches",
                columns: new[] { "CourseId", "AcademicYearId", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_Status_Year",
                table: "ResultSubmissionBatches",
                columns: new[] { "ApprovalStatus", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Programmes_Department_Level",
                table: "Programmes",
                columns: new[] { "DepartmentId", "ProgrammeLevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Programmes_Department_ModeOfStudy",
                table: "Programmes",
                columns: new[] { "DepartmentId", "ModeOfStudyId" });

            // REMOVED: IX_Programmes_DepartmentId - already exists as FK index

            migrationBuilder.CreateIndex(
                name: "IX_GradeConfigurations_Active_MinScore",
                table: "GradeConfigurations",
                columns: new[] { "IsActive", "MinScore" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeConfigurations_IsActive",
                table: "GradeConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_SchoolId_HODId",
                table: "Departments",
                columns: new[] { "SchoolId", "HODId" });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Programme_Year_Semester",
                table: "Courses",
                columns: new[] { "ProgrammeID", "YearTaken", "SemesterTaken" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_Programme_CurrentYear",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_Programme_Mode_Year",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_StudentExaminableCourses_Course_Year_Semester",
                table: "StudentExaminableCourses");

            migrationBuilder.DropIndex(
                name: "IX_StudentExaminableCourses_Full_Composite",
                table: "StudentExaminableCourses");

            migrationBuilder.DropIndex(
                name: "IX_StudentExaminableCourses_Status",
                table: "StudentExaminableCourses");

            migrationBuilder.DropIndex(
                name: "IX_StudentExaminableCourses_Student_Year_Semester",
                table: "StudentExaminableCourses");

            migrationBuilder.DropIndex(
                name: "IX_StudentAssessmentScores_Active_Course_Year",
                table: "StudentAssessmentScores");

            migrationBuilder.DropIndex(
                name: "IX_StudentAssessmentScores_Course_Year_Semester_Student",
                table: "StudentAssessmentScores");

            migrationBuilder.DropIndex(
                name: "IX_Schools_Dean_AssistantDean",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_ResultSubmissionBatches_ApprovalStatus",
                table: "ResultSubmissionBatches");

            migrationBuilder.DropIndex(
                name: "IX_ResultSubmissionBatches_Course_Status",
                table: "ResultSubmissionBatches");

            migrationBuilder.DropIndex(
                name: "IX_ResultSubmissionBatches_Course_Year_Semester",
                table: "ResultSubmissionBatches");

            migrationBuilder.DropIndex(
                name: "IX_ResultSubmissionBatches_Status_Year",
                table: "ResultSubmissionBatches");

            migrationBuilder.DropIndex(
                name: "IX_Programmes_Department_Level",
                table: "Programmes");

            migrationBuilder.DropIndex(
                name: "IX_Programmes_Department_ModeOfStudy",
                table: "Programmes");

            // REMOVED: IX_Programmes_DepartmentId - was never created by this migration

            migrationBuilder.DropIndex(
                name: "IX_GradeConfigurations_Active_MinScore",
                table: "GradeConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_GradeConfigurations_IsActive",
                table: "GradeConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_Departments_SchoolId_HODId",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Courses_Programme_Year_Semester",
                table: "Courses");

            migrationBuilder.RenameIndex(
                name: "IX_StudentAssessmentScores_RsbId",
                table: "StudentAssessmentScores",
                newName: "IX_StudentAssessmentScores_rsbId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_CourseId",
                table: "ResultSubmissionBatches",
                column: "CourseId");
        }
    }
}