using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class added_application_period : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicationPeriodId",
                table: "Programmes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApplicationPeriodId",
                table: "ModesOfStudy",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationPeriod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartOfApplication = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndOfApplication = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationPeriod", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Programmes_ApplicationPeriodId",
                table: "Programmes",
                column: "ApplicationPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_ModesOfStudy_ApplicationPeriodId",
                table: "ModesOfStudy",
                column: "ApplicationPeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModesOfStudy_ApplicationPeriod_ApplicationPeriodId",
                table: "ModesOfStudy",
                column: "ApplicationPeriodId",
                principalTable: "ApplicationPeriod",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Programmes_ApplicationPeriod_ApplicationPeriodId",
                table: "Programmes",
                column: "ApplicationPeriodId",
                principalTable: "ApplicationPeriod",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModesOfStudy_ApplicationPeriod_ApplicationPeriodId",
                table: "ModesOfStudy");

            migrationBuilder.DropForeignKey(
                name: "FK_Programmes_ApplicationPeriod_ApplicationPeriodId",
                table: "Programmes");

            migrationBuilder.DropTable(
                name: "ApplicationPeriod");

            migrationBuilder.DropIndex(
                name: "IX_Programmes_ApplicationPeriodId",
                table: "Programmes");

            migrationBuilder.DropIndex(
                name: "IX_ModesOfStudy_ApplicationPeriodId",
                table: "ModesOfStudy");

            migrationBuilder.DropColumn(
                name: "ApplicationPeriodId",
                table: "Programmes");

            migrationBuilder.DropColumn(
                name: "ApplicationPeriodId",
                table: "ModesOfStudy");
        }
    }
}
