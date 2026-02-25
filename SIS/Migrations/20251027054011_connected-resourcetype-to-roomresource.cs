using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class connectedresourcetypetoroomresource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing foreign key if it exists
            migrationBuilder.DropForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources");

            // Drop existing index if it exists
            migrationBuilder.DropIndex(
                name: "IX_RoomResources_ResourceTypeId",
                table: "RoomResources");

            // Drop Description and Name columns
            migrationBuilder.DropColumn(
                name: "Description",
                table: "RoomResources");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "RoomResources");

            // Make ResourceTypeId NOT NULL (it was nullable before)
            migrationBuilder.AlterColumn<int>(
                name: "ResourceTypeId",
                table: "RoomResources",
                type: "int",
                nullable: false,
                defaultValue: 1, // Default to first resource type
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // **REMOVED THE PROBLEMATIC AlterColumn on ResourceId**
            // We DON'T need to change ResourceId - it should remain as IDENTITY
            // The issue was EF trying to remove IDENTITY from ResourceId

            // Create new index on ResourceTypeId
            migrationBuilder.CreateIndex(
                name: "IX_RoomResources_ResourceTypeId",
                table: "RoomResources",
                column: "ResourceTypeId");

            // Add foreign key constraint - pointing to ResourceTypeId column
            migrationBuilder.AddForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "ResourceTypeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key
            migrationBuilder.DropForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources");

            // Drop index
            migrationBuilder.DropIndex(
                name: "IX_RoomResources_ResourceTypeId",
                table: "RoomResources");

            // Make ResourceTypeId nullable again
            migrationBuilder.AlterColumn<int>(
                name: "ResourceTypeId",
                table: "RoomResources",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Add back Name and Description columns
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "RoomResources",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "RoomResources",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Restore the old foreign key (if it was there)
            migrationBuilder.CreateIndex(
                name: "IX_RoomResources_ResourceTypeId",
                table: "RoomResources",
                column: "ResourceTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomResources_ResourceTypes_ResourceTypeId",
                table: "RoomResources",
                column: "ResourceTypeId",
                principalTable: "ResourceTypes",
                principalColumn: "ResourceTypeId");
        }
    }
}