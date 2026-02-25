using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class updateroomresourcetype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop the wrong foreign key constraint (if it exists)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_RoomResources_ResourceTypes_ResourceId')
                BEGIN
                    ALTER TABLE [RoomResources] DROP CONSTRAINT [FK_RoomResources_ResourceTypes_ResourceId]
                END
            ");

            // Step 2: Drop primary key constraint
            migrationBuilder.DropPrimaryKey(
                name: "PK_RoomResources",
                table: "RoomResources");

            // Step 3: Drop the ResourceId column (can't alter IDENTITY)
            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "RoomResources");

            // Step 4: Recreate ResourceId column with IDENTITY
            migrationBuilder.AddColumn<int>(
                name: "ResourceId",
                table: "RoomResources",
                type: "int",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            // Step 5: Restore primary key
            migrationBuilder.AddPrimaryKey(
                name: "PK_RoomResources",
                table: "RoomResources",
                column: "ResourceId");

            // Step 6: Create index for ResourceTypeId (if it doesn't exist)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RoomResources_ResourceTypeId' AND object_id = OBJECT_ID('RoomResources'))
                BEGIN
                    CREATE INDEX [IX_RoomResources_ResourceTypeId] ON [RoomResources] ([ResourceTypeId])
                END
            ");

            // Step 7: Add correct foreign key constraint (if it doesn't exist)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_RoomResources_ResourceTypes_ResourceTypeId')
                BEGIN
                    ALTER TABLE [RoomResources] 
                    ADD CONSTRAINT [FK_RoomResources_ResourceTypes_ResourceTypeId] 
                    FOREIGN KEY ([ResourceTypeId]) REFERENCES [ResourceTypes]([ResourceTypeId]) ON DELETE CASCADE
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: Drop new foreign key (if it exists)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_RoomResources_ResourceTypes_ResourceTypeId')
                BEGIN
                    ALTER TABLE [RoomResources] DROP CONSTRAINT [FK_RoomResources_ResourceTypes_ResourceTypeId]
                END
            ");

            // Drop new index (if it exists)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RoomResources_ResourceTypeId' AND object_id = OBJECT_ID('RoomResources'))
                BEGIN
                    DROP INDEX [IX_RoomResources_ResourceTypeId] ON [RoomResources]
                END
            ");

            // Drop primary key
            migrationBuilder.DropPrimaryKey(
                name: "PK_RoomResources",
                table: "RoomResources");

            // Drop IDENTITY column
            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "RoomResources");

            // Recreate without IDENTITY
            migrationBuilder.AddColumn<int>(
                name: "ResourceId",
                table: "RoomResources",
                type: "int",
                nullable: false);

            // Restore primary key
            migrationBuilder.AddPrimaryKey(
                name: "PK_RoomResources",
                table: "RoomResources",
                column: "ResourceId");

            // Restore wrong foreign key (conditionally)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_RoomResources_ResourceTypes_ResourceId')
                BEGIN
                    ALTER TABLE [RoomResources] 
                    ADD CONSTRAINT [FK_RoomResources_ResourceTypes_ResourceId] 
                    FOREIGN KEY ([ResourceId]) REFERENCES [ResourceTypes]([ResourceTypeId]) ON DELETE CASCADE
                END
            ");
        }
    }
}