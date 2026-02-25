using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantRegistrarToSchool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssistantRegistrarId",
                table: "Schools",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schools_AssistantRegistrarId",
                table: "Schools",
                column: "AssistantRegistrarId");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_AspNetUsers_AssistantRegistrarId",
                table: "Schools",
                column: "AssistantRegistrarId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schools_AspNetUsers_AssistantRegistrarId",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_Schools_AssistantRegistrarId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "AssistantRegistrarId",
                table: "Schools");
        }
    }
}
