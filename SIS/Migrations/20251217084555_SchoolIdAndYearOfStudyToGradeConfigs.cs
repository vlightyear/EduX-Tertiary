using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class SchoolIdAndYearOfStudyToGradeConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "GradeConfigurations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearOfStudy",
                table: "GradeConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "GradeConfigurations");

            migrationBuilder.DropColumn(
                name: "YearOfStudy",
                table: "GradeConfigurations");
        }
    }
}
