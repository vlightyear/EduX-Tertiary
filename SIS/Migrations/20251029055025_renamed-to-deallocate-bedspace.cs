using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class renamedtodeallocatebedspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReAllocateBedSpaceUponCheckOut",
                table: "AccomodationConfiguration",
                newName: "DeAllocateBedSpaceUponCheckOut");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DeAllocateBedSpaceUponCheckOut",
                table: "AccomodationConfiguration",
                newName: "ReAllocateBedSpaceUponCheckOut");
        }
    }
}
