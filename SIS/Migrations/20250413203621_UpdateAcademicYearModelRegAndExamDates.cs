using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAcademicYearModelRegAndExamDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "RegistrationEndDate",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationStartDate",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "RegistrationEndDate",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "RegistrationStartDate",
                table: "AcademicYears");
        }
    }
}
