using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetablingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleTrackings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimeSlotConfigId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsOccupied = table.Column<bool>(type: "bit", nullable: false),
                    OccupiedByCourseId = table.Column<int>(type: "int", nullable: true),
                    PeriodNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleTrackings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleTrackings_Courses_OccupiedByCourseId",
                        column: x => x.OccupiedByCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleTrackings_TimeSlotConfigurations_TimeSlotConfigId",
                        column: x => x.TimeSlotConfigId,
                        principalTable: "TimeSlotConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Timetables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    LearningRoomId = table.Column<int>(type: "int", nullable: false),
                    TimeSlotConfigId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    ModeOfStudyId = table.Column<int>(type: "int", nullable: false),
                    PeriodNumber = table.Column<int>(type: "int", nullable: false),
                    SpecialInstructions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    RecurrenceEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Timetables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Timetables_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Timetables_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Timetables_LearningRooms_LearningRoomId",
                        column: x => x.LearningRoomId,
                        principalTable: "LearningRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Timetables_ModesOfStudy_ModeOfStudyId",
                        column: x => x.ModeOfStudyId,
                        principalTable: "ModesOfStudy",
                        principalColumn: "ModeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Timetables_TimeSlotConfigurations_TimeSlotConfigId",
                        column: x => x.TimeSlotConfigId,
                        principalTable: "TimeSlotConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTrackings_EntityId_EntityType_Date_PeriodNumber",
                table: "ScheduleTrackings",
                columns: new[] { "EntityId", "EntityType", "Date", "PeriodNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTrackings_OccupiedByCourseId",
                table: "ScheduleTrackings",
                column: "OccupiedByCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTrackings_TimeSlotConfigId",
                table: "ScheduleTrackings",
                column: "TimeSlotConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Timetables_AcademicYearId_ModeOfStudyId_Date_PeriodNumber",
                table: "Timetables",
                columns: new[] { "AcademicYearId", "ModeOfStudyId", "Date", "PeriodNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Timetables_CourseId",
                table: "Timetables",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Timetables_LearningRoomId",
                table: "Timetables",
                column: "LearningRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Timetables_ModeOfStudyId",
                table: "Timetables",
                column: "ModeOfStudyId");

            migrationBuilder.CreateIndex(
                name: "IX_Timetables_Status",
                table: "Timetables",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Timetables_TimeSlotConfigId",
                table: "Timetables",
                column: "TimeSlotConfigId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleTrackings");

            migrationBuilder.DropTable(
                name: "Timetables");
        }
    }
}
