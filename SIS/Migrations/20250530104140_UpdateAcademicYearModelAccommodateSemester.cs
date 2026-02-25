using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAcademicYearModelAccommodateSemester : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYears_ModesOfStudy_ModeId",
                table: "AcademicYears");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_ModeId",
                table: "AcademicYears");

            migrationBuilder.AlterColumn<int>(
                name: "ModeId",
                table: "AcademicYears",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "AcademicType",
                table: "AcademicYears",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModeOfStudyModeId",
                table: "AcademicYears",
                type: "int",
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

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_ModeOfStudyModeId",
                table: "AcademicYears",
                column: "ModeOfStudyModeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicYears_ModesOfStudy_ModeOfStudyModeId",
                table: "AcademicYears",
                column: "ModeOfStudyModeId",
                principalTable: "ModesOfStudy",
                principalColumn: "ModeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYears_ModesOfStudy_ModeOfStudyModeId",
                table: "AcademicYears");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_ModeOfStudyModeId",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "AcademicType",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "ModeOfStudyModeId",
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

            migrationBuilder.AlterColumn<int>(
                name: "ModeId",
                table: "AcademicYears",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_ModeId",
                table: "AcademicYears",
                column: "ModeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicYears_ModesOfStudy_ModeId",
                table: "AcademicYears",
                column: "ModeId",
                principalTable: "ModesOfStudy",
                principalColumn: "ModeId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
