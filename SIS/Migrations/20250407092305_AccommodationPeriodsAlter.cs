using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AccommodationPeriodsAlter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "AccommodationPeriods");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "AccommodationPeriods",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "AccommodationPeriods",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<bool>(
                name: "AppliesUniversally",
                table: "AccommodationPeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FeeConfigurationId",
                table: "AccommodationPeriods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPermanentUntilGraduation",
                table: "AccommodationPeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ModeOfStudyId",
                table: "AccommodationPeriods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgramLevelId",
                table: "AccommodationPeriods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgrammeId",
                table: "AccommodationPeriods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "AccommodationPeriods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearOfStudy",
                table: "AccommodationPeriods",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_FeeConfigurationId",
                table: "AccommodationPeriods",
                column: "FeeConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_ModeOfStudyId",
                table: "AccommodationPeriods",
                column: "ModeOfStudyId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_ProgramLevelId",
                table: "AccommodationPeriods",
                column: "ProgramLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_ProgrammeId",
                table: "AccommodationPeriods",
                column: "ProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_SchoolId",
                table: "AccommodationPeriods",
                column: "SchoolId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationPeriods_FeeConfigurations_FeeConfigurationId",
                table: "AccommodationPeriods",
                column: "FeeConfigurationId",
                principalTable: "FeeConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationPeriods_ModesOfStudy_ModeOfStudyId",
                table: "AccommodationPeriods",
                column: "ModeOfStudyId",
                principalTable: "ModesOfStudy",
                principalColumn: "ModeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationPeriods_ProgramLevels_ProgramLevelId",
                table: "AccommodationPeriods",
                column: "ProgramLevelId",
                principalTable: "ProgramLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationPeriods_Programmes_ProgrammeId",
                table: "AccommodationPeriods",
                column: "ProgrammeId",
                principalTable: "Programmes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationPeriods_Schools_SchoolId",
                table: "AccommodationPeriods",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationPeriods_FeeConfigurations_FeeConfigurationId",
                table: "AccommodationPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationPeriods_ModesOfStudy_ModeOfStudyId",
                table: "AccommodationPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationPeriods_ProgramLevels_ProgramLevelId",
                table: "AccommodationPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationPeriods_Programmes_ProgrammeId",
                table: "AccommodationPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationPeriods_Schools_SchoolId",
                table: "AccommodationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationPeriods_FeeConfigurationId",
                table: "AccommodationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationPeriods_ModeOfStudyId",
                table: "AccommodationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationPeriods_ProgramLevelId",
                table: "AccommodationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationPeriods_ProgrammeId",
                table: "AccommodationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationPeriods_SchoolId",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "AppliesUniversally",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "FeeConfigurationId",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "IsPermanentUntilGraduation",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "ModeOfStudyId",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "ProgramLevelId",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "ProgrammeId",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "YearOfStudy",
                table: "AccommodationPeriods");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "AccommodationPeriods",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "AccommodationPeriods",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AccommodationPeriods",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
