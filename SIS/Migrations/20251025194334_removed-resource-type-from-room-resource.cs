using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class removedresourcetypefromroomresource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources");

            migrationBuilder.AlterColumn<int>(
                name: "ResourceTypeId",
                table: "RoomResources",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "ResourceTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources");

            migrationBuilder.AlterColumn<int>(
                name: "ResourceTypeId",
                table: "RoomResources",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "ResourceTypeId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
