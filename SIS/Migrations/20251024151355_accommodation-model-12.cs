using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class accommodationmodel12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationPeriods_FeeConfigurations_FeeConfigurationId",
                table: "AccommodationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationPeriods_FeeConfigurationId",
                table: "AccommodationPeriods");

            migrationBuilder.DropColumn(
                name: "FeeConfigurationId",
                table: "AccommodationPeriods");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FeeConfigurationId",
                table: "AccommodationPeriods",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_FeeConfigurationId",
                table: "AccommodationPeriods",
                column: "FeeConfigurationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationPeriods_FeeConfigurations_FeeConfigurationId",
                table: "AccommodationPeriods",
                column: "FeeConfigurationId",
                principalTable: "FeeConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
