using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class IsInternatinalStudentToFeeConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AppliesOnlyToForeignStudents",
                table: "FeeConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "FeeConfigurations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "FeeConfigurations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "FeeConfigurations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "FeeConfigurations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliesOnlyToForeignStudents",
                table: "FeeConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "FeeConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "FeeConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "FeeConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "FeeConfigurations");
        }
    }
}
