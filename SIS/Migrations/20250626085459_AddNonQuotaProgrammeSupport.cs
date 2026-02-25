using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddNonQuotaProgrammeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Programmes_DepartmentId",
                table: "Programmes");

            migrationBuilder.AddColumn<int>(
                name: "AssociatedNQProgrammeId",
                table: "Programmes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNonQuota",
                table: "Programmes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Programmes_AssociatedNQProgrammeId",
                table: "Programmes",
                column: "AssociatedNQProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_Programmes_Department_IsNonQuota",
                table: "Programmes",
                columns: new[] { "DepartmentId", "IsNonQuota" });

            migrationBuilder.CreateIndex(
                name: "IX_Programmes_IsNonQuota",
                table: "Programmes",
                column: "IsNonQuota");

            migrationBuilder.AddForeignKey(
                name: "FK_Programmes_Programmes_AssociatedNQProgrammeId",
                table: "Programmes",
                column: "AssociatedNQProgrammeId",
                principalTable: "Programmes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Programmes_Programmes_AssociatedNQProgrammeId",
                table: "Programmes");

            migrationBuilder.DropIndex(
                name: "IX_Programmes_AssociatedNQProgrammeId",
                table: "Programmes");

            migrationBuilder.DropIndex(
                name: "IX_Programmes_Department_IsNonQuota",
                table: "Programmes");

            migrationBuilder.DropIndex(
                name: "IX_Programmes_IsNonQuota",
                table: "Programmes");

            migrationBuilder.DropColumn(
                name: "AssociatedNQProgrammeId",
                table: "Programmes");

            migrationBuilder.DropColumn(
                name: "IsNonQuota",
                table: "Programmes");

            migrationBuilder.CreateIndex(
                name: "IX_Programmes_DepartmentId",
                table: "Programmes",
                column: "DepartmentId");
        }
    }
}
