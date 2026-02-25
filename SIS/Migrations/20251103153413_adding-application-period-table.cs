using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class addingapplicationperiodtable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applicants_ApplicationPeriod_ApplicationPeriodId",
                table: "Applicants");

            migrationBuilder.DropForeignKey(
                name: "FK_ModesOfStudy_ApplicationPeriod_ApplicationPeriodId",
                table: "ModesOfStudy");

            migrationBuilder.DropForeignKey(
                name: "FK_Programmes_ApplicationPeriod_ApplicationPeriodId",
                table: "Programmes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApplicationPeriod",
                table: "ApplicationPeriod");

            migrationBuilder.RenameTable(
                name: "ApplicationPeriod",
                newName: "ApplicationPeriods");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApplicationPeriods",
                table: "ApplicationPeriods",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Applicants_ApplicationPeriods_ApplicationPeriodId",
                table: "Applicants",
                column: "ApplicationPeriodId",
                principalTable: "ApplicationPeriods",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ModesOfStudy_ApplicationPeriods_ApplicationPeriodId",
                table: "ModesOfStudy",
                column: "ApplicationPeriodId",
                principalTable: "ApplicationPeriods",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Programmes_ApplicationPeriods_ApplicationPeriodId",
                table: "Programmes",
                column: "ApplicationPeriodId",
                principalTable: "ApplicationPeriods",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applicants_ApplicationPeriods_ApplicationPeriodId",
                table: "Applicants");

            migrationBuilder.DropForeignKey(
                name: "FK_ModesOfStudy_ApplicationPeriods_ApplicationPeriodId",
                table: "ModesOfStudy");

            migrationBuilder.DropForeignKey(
                name: "FK_Programmes_ApplicationPeriods_ApplicationPeriodId",
                table: "Programmes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApplicationPeriods",
                table: "ApplicationPeriods");

            migrationBuilder.RenameTable(
                name: "ApplicationPeriods",
                newName: "ApplicationPeriod");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApplicationPeriod",
                table: "ApplicationPeriod",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Applicants_ApplicationPeriod_ApplicationPeriodId",
                table: "Applicants",
                column: "ApplicationPeriodId",
                principalTable: "ApplicationPeriod",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ModesOfStudy_ApplicationPeriod_ApplicationPeriodId",
                table: "ModesOfStudy",
                column: "ApplicationPeriodId",
                principalTable: "ApplicationPeriod",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Programmes_ApplicationPeriod_ApplicationPeriodId",
                table: "Programmes",
                column: "ApplicationPeriodId",
                principalTable: "ApplicationPeriod",
                principalColumn: "Id");
        }
    }
}
