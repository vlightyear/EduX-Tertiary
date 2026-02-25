using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcademicEventTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IconName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicEventTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AcademicCalendarEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StartDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsAllDay = table.Column<bool>(type: "bit", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsSystemEvent = table.Column<bool>(type: "bit", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    EventTypeId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    ProgrammeId = table.Column<int>(type: "int", nullable: true),
                    ProgrammeLevelId = table.Column<int>(type: "int", nullable: true),
                    ModeOfStudyId = table.Column<int>(type: "int", nullable: true),
                    StudentYear = table.Column<int>(type: "int", nullable: true),
                    Semester = table.Column<int>(type: "int", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicCalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademicCalendarEvents_AcademicEventTypes_EventTypeId",
                        column: x => x.EventTypeId,
                        principalTable: "AcademicEventTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcademicCalendarEvents_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcademicCalendarEvents_ModesOfStudy_ModeOfStudyId",
                        column: x => x.ModeOfStudyId,
                        principalTable: "ModesOfStudy",
                        principalColumn: "ModeId");
                    table.ForeignKey(
                        name: "FK_AcademicCalendarEvents_ProgramLevels_ProgrammeLevelId",
                        column: x => x.ProgrammeLevelId,
                        principalTable: "ProgramLevels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AcademicCalendarEvents_Programmes_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalTable: "Programmes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AcademicCalendarEvents_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicCalendarEvents_AcademicYearId",
                table: "AcademicCalendarEvents",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicCalendarEvents_EventTypeId",
                table: "AcademicCalendarEvents",
                column: "EventTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicCalendarEvents_ModeOfStudyId",
                table: "AcademicCalendarEvents",
                column: "ModeOfStudyId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicCalendarEvents_ProgrammeId",
                table: "AcademicCalendarEvents",
                column: "ProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicCalendarEvents_ProgrammeLevelId",
                table: "AcademicCalendarEvents",
                column: "ProgrammeLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicCalendarEvents_SchoolId",
                table: "AcademicCalendarEvents",
                column: "SchoolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademicCalendarEvents");

            migrationBuilder.DropTable(
                name: "AcademicEventTypes");
        }
    }
}
