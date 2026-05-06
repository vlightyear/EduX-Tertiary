using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicYearPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeeConfigurations_AcademicYears_AcademicYearId",
                table: "FeeConfigurations");

            migrationBuilder.DropColumn(
                name: "FinalExamEndDate",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "FinalExamStartDate",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "GradeSubmissionEndDate",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "GradeSubmissionStartDate",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "Semester1EndDate",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "Semester1StartDate",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "Semester2EndDate",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "Semester2StartDate",
                table: "AcademicYears");

            migrationBuilder.RenameColumn(
                name: "CurrentSemester",
                table: "Students",
                newName: "CurrentYearPeriodId");

            migrationBuilder.RenameColumn(
                name: "Semester",
                table: "StudentInvoices",
                newName: "YearPeriodId");

            migrationBuilder.RenameIndex(
                name: "IX_StudentInvoice_Student_AcademicYear_Semester",
                table: "StudentInvoices",
                newName: "IX_StudentInvoice_Student_AcademicYear_YearPeriod");

            migrationBuilder.RenameColumn(
                name: "Semester",
                table: "StudentExaminableCourses",
                newName: "YearPeriodId");

            migrationBuilder.RenameColumn(
                name: "Semester",
                table: "StudentDisqualifications",
                newName: "YearPeriodId");

            migrationBuilder.RenameIndex(
                name: "IX_StudentDisqualifications_Student_Course_Year_Semester",
                table: "StudentDisqualifications",
                newName: "IX_StudentDisqualifications_Student_Course_Year_Period");

            migrationBuilder.RenameColumn(
                name: "Semester",
                table: "StudentCourseRegistrations",
                newName: "YearPeriodId");

            migrationBuilder.RenameColumn(
                name: "Semester",
                table: "StudentAssessmentScores",
                newName: "YearPeriodId");

            migrationBuilder.RenameColumn(
                name: "Semester",
                table: "ResultSubmissionBatches",
                newName: "YearPeriodId");

            migrationBuilder.RenameColumn(
                name: "Semester",
                table: "FeeConfigurations",
                newName: "YearPeriodId");

            migrationBuilder.RenameColumn(
                name: "SemesterTaken",
                table: "Courses",
                newName: "PeriodTakenId");

            migrationBuilder.RenameColumn(
                name: "CurrentStudentSemister",
                table: "BedSpaces",
                newName: "CurrentStudentPeriodId");

            migrationBuilder.AddColumn<int>(
                name: "AcademicPeriodId",
                table: "BedSpaces",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AcademicPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PeriodNumber = table.Column<int>(type: "int", nullable: false),
                    AcademicType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AcademicYearPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    AcademicPeriodId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExamStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExamEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegistrationStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegistrationEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GradeSubmissionStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GradeSubmissionEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYearPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademicYearPeriods_AcademicPeriods_AcademicPeriodId",
                        column: x => x.AcademicPeriodId,
                        principalTable: "AcademicPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicYearPeriods_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_CurrentYearPeriodId",
                table: "Students",
                column: "CurrentYearPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDisqualifications_YearPeriodId",
                table: "StudentDisqualifications",
                column: "YearPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeConfigurations_SchoolId",
                table: "GradeConfigurations",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeConfigurations_YearPeriodId",
                table: "FeeConfigurations",
                column: "YearPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_PeriodTakenId",
                table: "Courses",
                column: "PeriodTakenId");

            migrationBuilder.CreateIndex(
                name: "IX_BedSpaces_AcademicPeriodId",
                table: "BedSpaces",
                column: "AcademicPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYearPeriods_AcademicPeriodId",
                table: "AcademicYearPeriods",
                column: "AcademicPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYearPeriods_AcademicYearId_AcademicPeriodId",
                table: "AcademicYearPeriods",
                columns: new[] { "AcademicYearId", "AcademicPeriodId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BedSpaces_AcademicPeriods_AcademicPeriodId",
                table: "BedSpaces",
                column: "AcademicPeriodId",
                principalTable: "AcademicPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_AcademicPeriods_PeriodTakenId",
                table: "Courses",
                column: "PeriodTakenId",
                principalTable: "AcademicPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeConfigurations_AcademicYearPeriods_YearPeriodId",
                table: "FeeConfigurations",
                column: "YearPeriodId",
                principalTable: "AcademicYearPeriods",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FeeConfigurations_AcademicYears_AcademicYearId",
                table: "FeeConfigurations",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "YearId");

            migrationBuilder.AddForeignKey(
                name: "FK_GradeConfigurations_Schools_SchoolId",
                table: "GradeConfigurations",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentDisqualifications_AcademicYearPeriods_YearPeriodId",
                table: "StudentDisqualifications",
                column: "YearPeriodId",
                principalTable: "AcademicYearPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_AcademicYearPeriods_CurrentYearPeriodId",
                table: "Students",
                column: "CurrentYearPeriodId",
                principalTable: "AcademicYearPeriods",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BedSpaces_AcademicPeriods_AcademicPeriodId",
                table: "BedSpaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Courses_AcademicPeriods_PeriodTakenId",
                table: "Courses");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeConfigurations_AcademicYearPeriods_YearPeriodId",
                table: "FeeConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeConfigurations_AcademicYears_AcademicYearId",
                table: "FeeConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_GradeConfigurations_Schools_SchoolId",
                table: "GradeConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentDisqualifications_AcademicYearPeriods_YearPeriodId",
                table: "StudentDisqualifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_AcademicYearPeriods_CurrentYearPeriodId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "AcademicYearPeriods");

            migrationBuilder.DropTable(
                name: "AcademicPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Students_CurrentYearPeriodId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_StudentDisqualifications_YearPeriodId",
                table: "StudentDisqualifications");

            migrationBuilder.DropIndex(
                name: "IX_GradeConfigurations_SchoolId",
                table: "GradeConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_FeeConfigurations_YearPeriodId",
                table: "FeeConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_Courses_PeriodTakenId",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_BedSpaces_AcademicPeriodId",
                table: "BedSpaces");

            migrationBuilder.DropColumn(
                name: "AcademicPeriodId",
                table: "BedSpaces");

            migrationBuilder.RenameColumn(
                name: "CurrentYearPeriodId",
                table: "Students",
                newName: "CurrentSemester");

            migrationBuilder.RenameColumn(
                name: "YearPeriodId",
                table: "StudentInvoices",
                newName: "Semester");

            migrationBuilder.RenameIndex(
                name: "IX_StudentInvoice_Student_AcademicYear_YearPeriod",
                table: "StudentInvoices",
                newName: "IX_StudentInvoice_Student_AcademicYear_Semester");

            migrationBuilder.RenameColumn(
                name: "YearPeriodId",
                table: "StudentExaminableCourses",
                newName: "Semester");

            migrationBuilder.RenameColumn(
                name: "YearPeriodId",
                table: "StudentDisqualifications",
                newName: "Semester");

            migrationBuilder.RenameIndex(
                name: "IX_StudentDisqualifications_Student_Course_Year_Period",
                table: "StudentDisqualifications",
                newName: "IX_StudentDisqualifications_Student_Course_Year_Semester");

            migrationBuilder.RenameColumn(
                name: "YearPeriodId",
                table: "StudentCourseRegistrations",
                newName: "Semester");

            migrationBuilder.RenameColumn(
                name: "YearPeriodId",
                table: "StudentAssessmentScores",
                newName: "Semester");

            migrationBuilder.RenameColumn(
                name: "YearPeriodId",
                table: "ResultSubmissionBatches",
                newName: "Semester");

            migrationBuilder.RenameColumn(
                name: "YearPeriodId",
                table: "FeeConfigurations",
                newName: "Semester");

            migrationBuilder.RenameColumn(
                name: "PeriodTakenId",
                table: "Courses",
                newName: "SemesterTaken");

            migrationBuilder.RenameColumn(
                name: "CurrentStudentPeriodId",
                table: "BedSpaces",
                newName: "CurrentStudentSemister");

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalExamEndDate",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalExamStartDate",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradeSubmissionEndDate",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradeSubmissionStartDate",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Semester1EndDate",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Semester1StartDate",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Semester2EndDate",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Semester2StartDate",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeConfigurations_AcademicYears_AcademicYearId",
                table: "FeeConfigurations",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "YearId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
