using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class ChapterToAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChapterId",
                table: "AssessmentConfigurations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentConfigurations_ChapterId",
                table: "AssessmentConfigurations",
                column: "ChapterId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssessmentConfigurations_Chapters_ChapterId",
                table: "AssessmentConfigurations",
                column: "ChapterId",
                principalTable: "Chapters",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssessmentConfigurations_Chapters_ChapterId",
                table: "AssessmentConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_AssessmentConfigurations_ChapterId",
                table: "AssessmentConfigurations");

            migrationBuilder.DropColumn(
                name: "ChapterId",
                table: "AssessmentConfigurations");
        }
    }
}
