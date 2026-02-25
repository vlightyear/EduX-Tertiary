using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddAttemptSemesterToPorgressionRulesAndStudentAsssesssmentScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaximumFailedCourses",
                table: "ProgressionRules",
                newName: "PercentFailedOfCourseLoad");

            /*migrationBuilder.AddColumn<string>(
                name: "BatchReference",
                table: "StudentInvoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "StudentInvoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "StudentInvoices",
                type: "nvarchar(max)",
                nullable: true);*/

            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "StudentAssessmentScores",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "ProgressionRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Semester",
                table: "ProgressionRules",
                type: "int",
                nullable: true);

            /*migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "OnlinePayments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OtherFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliesOnlyToForeignStudents = table.Column<bool>(type: "bit", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: true),
                    CreditNCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DebitNCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AcademicYearId = table.Column<int>(type: "int", nullable: true),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    ProgrammeId = table.Column<int>(type: "int", nullable: true),
                    ModeOfStudyId = table.Column<int>(type: "int", nullable: true),
                    ProgramLevelId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherFees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtherFees_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId");
                    table.ForeignKey(
                        name: "FK_OtherFees_ModesOfStudy_ModeOfStudyId",
                        column: x => x.ModeOfStudyId,
                        principalTable: "ModesOfStudy",
                        principalColumn: "ModeId");
                    table.ForeignKey(
                        name: "FK_OtherFees_ProgramLevels_ProgramLevelId",
                        column: x => x.ProgramLevelId,
                        principalTable: "ProgramLevels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OtherFees_Programmes_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalTable: "Programmes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OtherFees_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OtherFees_AcademicYearId",
                table: "OtherFees",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherFees_ModeOfStudyId",
                table: "OtherFees",
                column: "ModeOfStudyId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherFees_ProgramLevelId",
                table: "OtherFees",
                column: "ProgramLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherFees_ProgrammeId",
                table: "OtherFees",
                column: "ProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherFees_SchoolId",
                table: "OtherFees",
                column: "SchoolId");*/
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtherFees");

            migrationBuilder.DropColumn(
                name: "BatchReference",
                table: "StudentInvoices");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "StudentInvoices");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "StudentInvoices");

            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "StudentAssessmentScores");

            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "ProgressionRules");

            migrationBuilder.DropColumn(
                name: "Semester",
                table: "ProgressionRules");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "OnlinePayments");

            migrationBuilder.RenameColumn(
                name: "PercentFailedOfCourseLoad",
                table: "ProgressionRules",
                newName: "MaximumFailedCourses");
        }
    }
}
