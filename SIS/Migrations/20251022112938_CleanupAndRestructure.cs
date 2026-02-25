using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class CleanupAndRestructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // REMOVED: migrationBuilder.DropForeignKey for FK_AcademicYears_ModesOfStudy_ModeId (doesn't exist)

            migrationBuilder.DropForeignKey(
                name: "FK_FeeConfigurations_ModesOfStudy_ModeOfStudyId",
                table: "FeeConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources");

            migrationBuilder.DropForeignKey(
                name: "FK_Timetables_ModesOfStudy_ModeOfStudyId",
                table: "Timetables");

            // REMOVED: DropIndex for IX_Programmes_DepartmentId (doesn't exist)
            // REMOVED: DropIndex for IX_AcademicYears_ModeId (doesn't exist)
            // REMOVED: AddColumn PassportPhotoPath to Students (already exists)
            // REMOVED: AddColumn Description to RoomResources (already exists)
            // REMOVED: AddColumn Name to RoomResources (already exists)
            // REMOVED: AddColumn SchoolId to ProgressionRules (already exists)
            // REMOVED: AddColumn AssociatedNQProgrammeId to Programmes (already exists)
            // REMOVED: AddColumn IsNonQuota to Programmes (already exists)
            // REMOVED: AddColumn IsSemesterBased to Programmes (already exists)
            // REMOVED: AddColumn Semester to FinancialStatements (already exists)
            // REMOVED: AddColumn AppliesOnlyToForeignStudents to FeeConfigurations (already exists)
            // REMOVED: AddColumn CreatedAt to FeeConfigurations (already exists)
            // REMOVED: AddColumn CreatedBy to FeeConfigurations (already exists)
            // REMOVED: AddColumn CreditNCode to FeeConfigurations (already exists)
            // REMOVED: AddColumn DebitNCode to FeeConfigurations (already exists)
            // REMOVED: AddColumn Semester to FeeConfigurations (already exists - duplicate)
            // REMOVED: AddColumn UpdatedAt to FeeConfigurations (already exists)
            // REMOVED: AddColumn UpdatedBy to FeeConfigurations (already exists)
            // REMOVED: AddColumn PassportPhotoPath to Applicants (already exists)
            // REMOVED: AddColumn StudentId to Allocations (already exists)
            // REMOVED: AddColumn AcademicType to AcademicYears (already exists)
            // REMOVED: AddColumn Semester1EndDate to AcademicYears (already exists)
            // REMOVED: AddColumn Semester1StartDate to AcademicYears (already exists)
            // REMOVED: AddColumn Semester2EndDate to AcademicYears (already exists)
            // REMOVED: AddColumn Semester2StartDate to AcademicYears (already exists)

            migrationBuilder.CreateIndex(
                name: "IX_Programmes_DepartmentId",
                table: "Programmes",
                column: "DepartmentId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_FeeConfigurations_ModesOfStudy_ModeOfStudyId",
                table: "FeeConfigurations",
                column: "ModeOfStudyId",
                principalTable: "ModesOfStudy",
                principalColumn: "ModeId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "ResourceTypeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Timetables_ModesOfStudy_ModeOfStudyId",
                table: "Timetables",
                column: "ModeOfStudyId",
                principalTable: "ModesOfStudy",
                principalColumn: "ModeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYears_ModesOfStudy_ModeId",
                table: "AcademicYears");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeConfigurations_ModesOfStudy_ModeOfStudyId",
                table: "FeeConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources");

            migrationBuilder.DropForeignKey(
                name: "FK_Timetables_ModesOfStudy_ModeOfStudyId",
                table: "Timetables");

            migrationBuilder.DropIndex(
                name: "IX_Programmes_DepartmentId",
                table: "Programmes");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_ModeId",
                table: "AcademicYears");

            // REMOVED: DropColumn PassportPhotoPath from Students (already exists)
            // REMOVED: DropColumn Description from RoomResources (already exists)
            // REMOVED: DropColumn Name from RoomResources (already exists)
            // REMOVED: DropColumn SchoolId from ProgressionRules (already exists)
            // REMOVED: DropColumn AssociatedNQProgrammeId from Programmes (already exists)
            // REMOVED: DropColumn IsNonQuota from Programmes (already exists)
            // REMOVED: DropColumn IsSemesterBased from Programmes (already exists)
            // REMOVED: DropColumn Semester from FinancialStatements (already exists)
            // REMOVED: DropColumn AppliesOnlyToForeignStudents from FeeConfigurations (already exists)
            // REMOVED: DropColumn CreatedAt from FeeConfigurations (already exists)
            // REMOVED: DropColumn CreatedBy from FeeConfigurations (already exists)
            // REMOVED: DropColumn CreditNCode from FeeConfigurations (already exists)
            // REMOVED: DropColumn DebitNCode from FeeConfigurations (already exists)
            // REMOVED: DropColumn Semester from FeeConfigurations (already exists - duplicate)
            // REMOVED: DropColumn UpdatedAt from FeeConfigurations (already exists)
            // REMOVED: DropColumn UpdatedBy from FeeConfigurations (already exists)
            // REMOVED: DropColumn PassportPhotoPath from Applicants (already exists)
            // REMOVED: DropColumn StudentId from Allocations (already exists)
            // REMOVED: DropColumn AcademicType from AcademicYears (already exists)
            // REMOVED: DropColumn Semester1EndDate from AcademicYears (already exists)
            // REMOVED: DropColumn Semester1StartDate from AcademicYears (already exists)
            // REMOVED: DropColumn Semester2EndDate from AcademicYears (already exists)
            // REMOVED: DropColumn Semester2StartDate from AcademicYears (already exists)

            migrationBuilder.AlterColumn<int>(
                name: "AcademicYearId",
                table: "FeeConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PrimarySchoolPeriod",
                table: "Applicants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PrimarySchoolName",
                table: "Applicants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PrimarySchoolAddress",
                table: "Applicants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

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
                name: "IX_Programmes_DepartmentId",
                table: "Programmes",
                column: "DepartmentId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_FeeConfigurations_ModesOfStudy_ModeOfStudyId",
                table: "FeeConfigurations",
                column: "ModeOfStudyId",
                principalTable: "ModesOfStudy",
                principalColumn: "ModeId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "ResourceTypeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Timetables_ModesOfStudy_ModeOfStudyId",
                table: "Timetables",
                column: "ModeOfStudyId",
                principalTable: "ModesOfStudy",
                principalColumn: "ModeId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}