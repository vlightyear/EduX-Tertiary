using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddYearOfStudyToScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "YearOfStudy",
                table: "StudentAssessmentScores",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "YearOfStudy",
                table: "StudentAssessmentScores");
        }
    }
}
