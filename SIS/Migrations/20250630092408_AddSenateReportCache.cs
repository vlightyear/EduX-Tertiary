using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddSenateReportCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SenateReportCaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReportData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SenateReportCaches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SenateReportCaches_ExpiresAt",
                table: "SenateReportCaches",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SenateReportCaches_ReportKey",
                table: "SenateReportCaches",
                column: "ReportKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SenateReportCaches");
        }
    }
}
