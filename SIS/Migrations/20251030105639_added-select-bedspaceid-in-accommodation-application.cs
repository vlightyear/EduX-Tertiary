using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class addedselectbedspaceidinaccommodationapplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SelectedBedId",
                table: "AccommodationApplications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationApplications_SelectedBedId",
                table: "AccommodationApplications",
                column: "SelectedBedId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationApplications_BedSpaces_SelectedBedId",
                table: "AccommodationApplications",
                column: "SelectedBedId",
                principalTable: "BedSpaces",
                principalColumn: "BedId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationApplications_BedSpaces_SelectedBedId",
                table: "AccommodationApplications");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationApplications_SelectedBedId",
                table: "AccommodationApplications");

            migrationBuilder.DropColumn(
                name: "SelectedBedId",
                table: "AccommodationApplications");
        }
    }
}
