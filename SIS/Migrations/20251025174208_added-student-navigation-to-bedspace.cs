using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class addedstudentnavigationtobedspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_BedId",
                table: "Students");

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "BedSpaces",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_BedId",
                table: "Students",
                column: "BedId",
                unique: true,
                filter: "[BedId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BedSpaces_StudentId",
                table: "BedSpaces",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_BedSpaces_Students_StudentId",
                table: "BedSpaces",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BedSpaces_Students_StudentId",
                table: "BedSpaces");

            migrationBuilder.DropIndex(
                name: "IX_Students_BedId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_BedSpaces_StudentId",
                table: "BedSpaces");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "BedSpaces");

            migrationBuilder.CreateIndex(
                name: "IX_Students_BedId",
                table: "Students",
                column: "BedId");
        }
    }
}
