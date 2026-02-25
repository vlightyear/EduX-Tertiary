using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddChaptersToContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingsAttendance_ZoomMeetings_ZoomMeetingId",
                table: "MeetingsAttendance");

            migrationBuilder.AddColumn<int>(
                name: "ChapterId",
                table: "CourseContents",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Chapters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chapters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chapters_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseContents_ChapterId",
                table: "CourseContents",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_CourseId",
                table: "Chapters",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseContents_Chapters_ChapterId",
                table: "CourseContents",
                column: "ChapterId",
                principalTable: "Chapters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingsAttendance_ZoomMeetings_ZoomMeetingId",
                table: "MeetingsAttendance",
                column: "ZoomMeetingId",
                principalTable: "ZoomMeetings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseContents_Chapters_ChapterId",
                table: "CourseContents");

            migrationBuilder.DropForeignKey(
                name: "FK_MeetingsAttendance_ZoomMeetings_ZoomMeetingId",
                table: "MeetingsAttendance");

            migrationBuilder.DropTable(
                name: "Chapters");

            migrationBuilder.DropIndex(
                name: "IX_CourseContents_ChapterId",
                table: "CourseContents");

            migrationBuilder.DropColumn(
                name: "ChapterId",
                table: "CourseContents");

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingsAttendance_ZoomMeetings_ZoomMeetingId",
                table: "MeetingsAttendance",
                column: "ZoomMeetingId",
                principalTable: "ZoomMeetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
