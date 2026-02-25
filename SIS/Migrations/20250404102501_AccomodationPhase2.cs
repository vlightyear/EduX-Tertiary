using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AccomodationPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccommodationPeriods",
                columns: table => new
                {
                    PeriodId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicationStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApplicationEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationPeriods", x => x.PeriodId);
                    table.ForeignKey(
                        name: "FK_AccommodationPeriods_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationApplications",
                columns: table => new
                {
                    ApplicationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    PeriodId = table.Column<int>(type: "int", nullable: false),
                    ApplicationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationApplications", x => x.ApplicationId);
                    table.ForeignKey(
                        name: "FK_AccommodationApplications_AccommodationPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "AccommodationPeriods",
                        principalColumn: "PeriodId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccommodationApplications_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Allocations",
                columns: table => new
                {
                    AllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    BedId = table.Column<int>(type: "int", nullable: false),
                    AllocationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllocatedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsGraduationBased = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allocations", x => x.AllocationId);
                    table.ForeignKey(
                        name: "FK_Allocations_AccommodationApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "AccommodationApplications",
                        principalColumn: "ApplicationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Allocations_AspNetUsers_AllocatedById",
                        column: x => x.AllocatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Allocations_BedSpaces_BedId",
                        column: x => x.BedId,
                        principalTable: "BedSpaces",
                        principalColumn: "BedId");
                });

            migrationBuilder.CreateTable(
                name: "CheckInOuts",
                columns: table => new
                {
                    CheckId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AllocationId = table.Column<int>(type: "int", nullable: false),
                    CheckInDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckInCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheckInStaffId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CheckOutDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckOutCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheckOutStaffId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DamageCharges = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInOuts", x => x.CheckId);
                    table.ForeignKey(
                        name: "FK_CheckInOuts_Allocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "Allocations",
                        principalColumn: "AllocationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckInOuts_AspNetUsers_CheckInStaffId",
                        column: x => x.CheckInStaffId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CheckInOuts_AspNetUsers_CheckOutStaffId",
                        column: x => x.CheckOutStaffId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationApplications_PeriodId",
                table: "AccommodationApplications",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationApplications_StudentId",
                table: "AccommodationApplications",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationPeriods_AcademicYearId",
                table: "AccommodationPeriods",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_AllocatedById",
                table: "Allocations",
                column: "AllocatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_ApplicationId",
                table: "Allocations",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_BedId",
                table: "Allocations",
                column: "BedId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInOuts_AllocationId",
                table: "CheckInOuts",
                column: "AllocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckInOuts_CheckInStaffId",
                table: "CheckInOuts",
                column: "CheckInStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInOuts_CheckOutStaffId",
                table: "CheckInOuts",
                column: "CheckOutStaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckInOuts");

            migrationBuilder.DropTable(
                name: "Allocations");

            migrationBuilder.DropTable(
                name: "AccommodationApplications");

            migrationBuilder.DropTable(
                name: "AccommodationPeriods");
        }
    }
}
