using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicYearToGradeConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "YearOfStudy",
                table: "GradeConfigurations");

            migrationBuilder.AddColumn<int>(
                name: "AcacdemicYearId",
                table: "GradeConfigurations",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcacdemicYearId",
                table: "GradeConfigurations");

            migrationBuilder.AddColumn<int>(
                name: "YearOfStudy",
                table: "GradeConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
