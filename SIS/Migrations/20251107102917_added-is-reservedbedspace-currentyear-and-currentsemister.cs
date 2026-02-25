using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class addedisreservedbedspacecurrentyearandcurrentsemister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentStudentSemister",
                table: "BedSpaces",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentStudentYear",
                table: "BedSpaces",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSpecialReservation",
                table: "BedSpaces",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentStudentSemister",
                table: "BedSpaces");

            migrationBuilder.DropColumn(
                name: "CurrentStudentYear",
                table: "BedSpaces");

            migrationBuilder.DropColumn(
                name: "IsSpecialReservation",
                table: "BedSpaces");
        }
    }
}
