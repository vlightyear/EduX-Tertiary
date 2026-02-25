using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class nowfollowingallocationtofindbedspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop FK that exists (this one worked in your log)
            migrationBuilder.DropForeignKey(
                name: "FK_AccommodationApplications_BedSpaces_BedId",
                table: "AccommodationApplications");

            // Drop FK for BedSpaces (check if exists first with SQL)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_BedSpaces_Students_StudentId')
                ALTER TABLE BedSpaces DROP CONSTRAINT FK_BedSpaces_Students_StudentId;
            ");

            // Drop FK for Allocations
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Allocations_AspNetUsers_StudentId')
                ALTER TABLE Allocations DROP CONSTRAINT FK_Allocations_AspNetUsers_StudentId;
            ");

            // Drop indexes (safe approach)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BedSpaces_StudentId' AND object_id = OBJECT_ID('BedSpaces'))
                DROP INDEX IX_BedSpaces_StudentId ON BedSpaces;
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Allocations_StudentId' AND object_id = OBJECT_ID('Allocations'))
                DROP INDEX IX_Allocations_StudentId ON Allocations;
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AccommodationApplications_BedId' AND object_id = OBJECT_ID('AccommodationApplications'))
                DROP INDEX IX_AccommodationApplications_BedId ON AccommodationApplications;
            ");

            // Drop columns (safe approach)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE name = 'StudentId' AND object_id = OBJECT_ID('BedSpaces'))
                ALTER TABLE BedSpaces DROP COLUMN StudentId;
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE name = 'StudentId' AND object_id = OBJECT_ID('Allocations'))
                ALTER TABLE Allocations DROP COLUMN StudentId;
            ");

            // Drop default constraint first, then drop column for AccommodationApplications.BedId
            migrationBuilder.Sql(@"
                -- Drop default constraint if it exists
                DECLARE @ConstraintName nvarchar(200)
                SELECT @ConstraintName = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON dc.parent_column_id = c.column_id AND dc.parent_object_id = c.object_id
                WHERE c.name = 'BedId' AND OBJECT_NAME(c.object_id) = 'AccommodationApplications'
                
                IF @ConstraintName IS NOT NULL
                    EXEC('ALTER TABLE AccommodationApplications DROP CONSTRAINT ' + @ConstraintName)
                
                -- Now drop the column if it exists
                IF EXISTS (SELECT * FROM sys.columns WHERE name = 'BedId' AND object_id = OBJECT_ID('AccommodationApplications'))
                    ALTER TABLE AccommodationApplications DROP COLUMN BedId;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore columns
            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "BedSpaces",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentId",
                table: "Allocations",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BedId",
                table: "AccommodationApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Restore indexes
            migrationBuilder.CreateIndex(
                name: "IX_BedSpaces_StudentId",
                table: "BedSpaces",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_StudentId",
                table: "Allocations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationApplications_BedId",
                table: "AccommodationApplications",
                column: "BedId");

            // Restore foreign keys
            migrationBuilder.AddForeignKey(
                name: "FK_AccommodationApplications_BedSpaces_BedId",
                table: "AccommodationApplications",
                column: "BedId",
                principalTable: "BedSpaces",
                principalColumn: "BedId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Allocations_AspNetUsers_StudentId",
                table: "Allocations",
                column: "StudentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BedSpaces_Students_StudentId",
                table: "BedSpaces",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id");
        }
    }
}