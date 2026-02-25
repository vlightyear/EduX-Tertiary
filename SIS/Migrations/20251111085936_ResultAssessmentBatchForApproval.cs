using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class ResultAssessmentBatchForApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rsbId",
                table: "StudentAssessmentScores",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResultSubmissionBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    AssessmentId = table.Column<int>(type: "int", nullable: true),
                    SubmissionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UploadedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    TotalRecords = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    WorkflowInstanceId = table.Column<int>(type: "int", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BatchHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AssessmentScoreIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CourseResultIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedForApprovalAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultSubmissionBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultSubmissionBatches_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResultSubmissionBatches_AspNetUsers_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ResultSubmissionBatches_AspNetUsers_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResultSubmissionBatches_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ResultSubmissionBatches_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResultSubmissionBatches_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_rsbId",
                table: "StudentAssessmentScores",
                column: "rsbId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_AcademicYearId",
                table: "ResultSubmissionBatches",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_ApprovedById",
                table: "ResultSubmissionBatches",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_AssessmentId",
                table: "ResultSubmissionBatches",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_CourseId",
                table: "ResultSubmissionBatches",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_UploadedById",
                table: "ResultSubmissionBatches",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_ResultSubmissionBatches_WorkflowInstanceId",
                table: "ResultSubmissionBatches",
                column: "WorkflowInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentAssessmentScores_ResultSubmissionBatches_rsbId",
                table: "StudentAssessmentScores",
                column: "rsbId",
                principalTable: "ResultSubmissionBatches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentAssessmentScores_ResultSubmissionBatches_rsbId",
                table: "StudentAssessmentScores");

            migrationBuilder.DropTable(
                name: "ResultSubmissionBatches");

            migrationBuilder.DropIndex(
                name: "IX_StudentAssessmentScores_rsbId",
                table: "StudentAssessmentScores");

            migrationBuilder.DropColumn(
                name: "rsbId",
                table: "StudentAssessmentScores");
        }
    }
}
