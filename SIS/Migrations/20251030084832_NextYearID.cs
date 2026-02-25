using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class NextYearID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextAcademicYearId",
                table: "AcademicYears",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_NextAcademicYearId",
                table: "AcademicYears",
                column: "NextAcademicYearId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicYears_AcademicYears_NextAcademicYearId",
                table: "AcademicYears",
                column: "NextAcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "YearId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicYears_AcademicYears_NextAcademicYearId",
                table: "AcademicYears");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_NextAcademicYearId",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "NextAcademicYearId",
                table: "AcademicYears");
        }
    }
}
