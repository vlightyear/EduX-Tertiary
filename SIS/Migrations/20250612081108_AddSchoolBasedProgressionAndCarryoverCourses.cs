using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolBasedProgressionAndCarryoverCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "ProgressionRules",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudentCarryoverCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    OriginalAcademicYearId = table.Column<int>(type: "int", nullable: false),
                    OriginalSemester = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CarryoverDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCarryoverCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCarryoverCourses_AcademicYears_OriginalAcademicYearId",
                        column: x => x.OriginalAcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCarryoverCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCarryoverCourses_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgressionRule_SchoolId_MaxFailed_IsActive",
                table: "ProgressionRules",
                columns: new[] { "SchoolId", "MaximumFailedCourses", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentCarryover_Student_Active",
                table: "StudentCarryoverCourses",
                columns: new[] { "StudentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentCarryover_Student_Course_Active",
                table: "StudentCarryoverCourses",
                columns: new[] { "StudentId", "CourseId", "IsActive" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCarryoverCourses_CourseId",
                table: "StudentCarryoverCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCarryoverCourses_OriginalAcademicYearId",
                table: "StudentCarryoverCourses",
                column: "OriginalAcademicYearId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressionRules_Schools_SchoolId",
                table: "ProgressionRules",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgressionRules_Schools_SchoolId",
                table: "ProgressionRules");

            migrationBuilder.DropTable(
                name: "StudentCarryoverCourses");

            migrationBuilder.DropIndex(
                name: "IX_ProgressionRule_SchoolId_MaxFailed_IsActive",
                table: "ProgressionRules");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "ProgressionRules");
        }
    }
}
