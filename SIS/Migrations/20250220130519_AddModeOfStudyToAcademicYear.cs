using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddModeOfStudyToAcademicYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModeId",
                table: "AcademicYears",
                type: "int",
                nullable: false,
                defaultValue: 1);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYears_ModesOfStudy_ModeId",
                table: "AcademicYears");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_ModeId",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "ModeId",
                table: "AcademicYears");
        }
    }
}
