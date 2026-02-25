using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddZoomMeetingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZoomMeetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    ZoomMeetingId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    JoinUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StartUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Password = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Agenda = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoomMeetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZoomMeetings_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZoomMeetings_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingsAttendance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZoomMeetingId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingsAttendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingsAttendance_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingsAttendance_ZoomMeetings_ZoomMeetingId",
                        column: x => x.ZoomMeetingId,
                        principalTable: "ZoomMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingsAttendance_StudentId",
                table: "MeetingsAttendance",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingsAttendance_ZoomMeetingId",
                table: "MeetingsAttendance",
                column: "ZoomMeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoomMeetings_CourseId",
                table: "ZoomMeetings",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoomMeetings_CreatedById",
                table: "ZoomMeetings",
                column: "CreatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingsAttendance");

            migrationBuilder.DropTable(
                name: "ZoomMeetings");
        }
    }
}
