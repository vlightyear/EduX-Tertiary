using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddDeanFieldsToSchool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssistantDeanId",
                table: "Schools",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeanId",
                table: "Schools",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schools_AssistantDeanId",
                table: "Schools",
                column: "AssistantDeanId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_DeanId",
                table: "Schools",
                column: "DeanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_AspNetUsers_AssistantDeanId",
                table: "Schools",
                column: "AssistantDeanId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_AspNetUsers_DeanId",
                table: "Schools",
                column: "DeanId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schools_AspNetUsers_AssistantDeanId",
                table: "Schools");

            migrationBuilder.DropForeignKey(
                name: "FK_Schools_AspNetUsers_DeanId",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_Schools_AssistantDeanId",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_Schools_DeanId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "AssistantDeanId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "DeanId",
                table: "Schools");
        }
    }
}
