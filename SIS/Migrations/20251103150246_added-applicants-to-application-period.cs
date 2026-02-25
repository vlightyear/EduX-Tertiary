using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class addedapplicantstoapplicationperiod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicationPeriodId",
                table: "Applicants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_ApplicationPeriodId",
                table: "Applicants",
                column: "ApplicationPeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_Applicants_ApplicationPeriod_ApplicationPeriodId",
                table: "Applicants",
                column: "ApplicationPeriodId",
                principalTable: "ApplicationPeriod",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applicants_ApplicationPeriod_ApplicationPeriodId",
                table: "Applicants");

            migrationBuilder.DropIndex(
                name: "IX_Applicants_ApplicationPeriodId",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "ApplicationPeriodId",
                table: "Applicants");
        }
    }
}
