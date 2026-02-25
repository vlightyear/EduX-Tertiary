using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class StudentDisqualification : Migration
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

            migrationBuilder.CreateTable(
                name: "StudentDisqualifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    DisqualificationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IncidentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisqualificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PenaltyDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PenaltyDurationSemesters = table.Column<int>(type: "int", nullable: true),
                    IsBannedFromCourse = table.Column<bool>(type: "bit", nullable: false),
                    IsSuspended = table.Column<bool>(type: "bit", nullable: false),
                    AppealDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppealDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AppealStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AppealDecision = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AppealDecisionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StudentId1 = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDisqualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentDisqualifications_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentDisqualifications_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentDisqualifications_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentDisqualifications_Students_StudentId1",
                        column: x => x.StudentId1,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_StudentDisqualifications_AcademicYearId",
                table: "StudentDisqualifications",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDisqualifications_CourseId",
                table: "StudentDisqualifications",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDisqualifications_Status",
                table: "StudentDisqualifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDisqualifications_Student_Course_Year_Semester",
                table: "StudentDisqualifications",
                columns: new[] { "StudentId", "CourseId", "AcademicYearId", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentDisqualifications_StudentId",
                table: "StudentDisqualifications",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDisqualifications_StudentId1",
                table: "StudentDisqualifications",
                column: "StudentId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentDisqualifications");

            migrationBuilder.DropTable(
                name: "vw_StudentResults");

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
