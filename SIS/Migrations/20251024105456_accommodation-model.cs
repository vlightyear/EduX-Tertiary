using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class accommodationmodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_accomodationConfigurations",
                table: "accomodationConfigurations");

            migrationBuilder.RenameTable(
                name: "accomodationConfigurations",
                newName: "AccomodationConfiguration");

            migrationBuilder.AddColumn<DateTime>(
                name: "BedAllocationEndDate",
                table: "Students",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BedId",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BedId",
                table: "AccommodationApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LocationToTakeAccommodationPaymentReceipt",
                table: "AccomodationConfiguration",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AccomodationConfiguration",
                table: "AccomodationConfiguration",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Students_BedId",
                table: "Students",
                column: "BedId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationApplications_BedId",
                table: "AccommodationApplications",
                column: "BedId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationApplications_BedSpaces_BedId",
                table: "AccommodationApplications",
                column: "BedId",
                principalTable: "BedSpaces",
                principalColumn: "BedId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_BedSpaces_BedId",
                table: "Students",
                column: "BedId",
                principalTable: "BedSpaces",
                principalColumn: "BedId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationApplications_BedSpaces_BedId",
                table: "AccommodationApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_BedSpaces_BedId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_BedId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationApplications_BedId",
                table: "AccommodationApplications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AccomodationConfiguration",
                table: "AccomodationConfiguration");

            migrationBuilder.DropColumn(
                name: "BedAllocationEndDate",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "BedId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "BedId",
                table: "AccommodationApplications");

            migrationBuilder.DropColumn(
                name: "LocationToTakeAccommodationPaymentReceipt",
                table: "AccomodationConfiguration");

            migrationBuilder.RenameTable(
                name: "AccomodationConfiguration",
                newName: "accomodationConfigurations");

            migrationBuilder.AddPrimaryKey(
                name: "PK_accomodationConfigurations",
                table: "accomodationConfigurations",
                column: "Id");
        }
    }
}
