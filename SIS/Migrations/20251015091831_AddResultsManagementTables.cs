using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddResultsManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResultAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IPAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsBatchOperation = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    BatchId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OldValueHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NewValueHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultAuditLogs_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultAuditLogs_AspNetUsers_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultAuditLogs_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultAuditLogs_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentCourseResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    WeightedTotal = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    NormalizedTotal = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    GradeLetter = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    GradePoints = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    IsPassed = table.Column<bool>(type: "bit", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    CreditsEarned = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 38),
                    ResultHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PublishedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CalculatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PassMark = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TotalWeightPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AssessmentCount = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsCarryover = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCourseResults", x => x.Id);
                    table.UniqueConstraint("AK_StudentCourseResults_StudentId_CourseId_AcademicYearId", x => new { x.StudentId, x.CourseId, x.AcademicYearId });
                    table.ForeignKey(
                        name: "FK_StudentCourseResults_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCourseResults_AspNetUsers_CalculatedBy",
                        column: x => x.CalculatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCourseResults_AspNetUsers_PublishedBy",
                        column: x => x.PublishedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCourseResults_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCourseResults_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAssessmentScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    AssessmentId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 100m),
                    WeightPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScoreHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAssessmentScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAssessmentScores_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAssessmentScores_AspNetUsers_ModifiedBy",
                        column: x => x.ModifiedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAssessmentScores_AspNetUsers_RecordedBy",
                        column: x => x.RecordedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAssessmentScores_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAssessmentScores_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAssessmentScores_StudentCourseResults_StudentId_CourseId_AcademicYearId",
                        columns: x => new { x.StudentId, x.CourseId, x.AcademicYearId },
                        principalTable: "StudentCourseResults",
                        principalColumns: new[] { "StudentId", "CourseId", "AcademicYearId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentAssessmentScores_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResultAuditLogs_ActionType",
                table: "ResultAuditLogs",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAuditLogs_BatchId",
                table: "ResultAuditLogs",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAuditLogs_ChangedAt",
                table: "ResultAuditLogs",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAuditLogs_ChangedBy",
                table: "ResultAuditLogs",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAuditLogs_CourseId",
                table: "ResultAuditLogs",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAuditLogs_Entity",
                table: "ResultAuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultAuditLogs_Student_Course",
                table: "ResultAuditLogs",
                columns: new[] { "StudentId", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultAuditLogs_Year_Action_Date",
                table: "ResultAuditLogs",
                columns: new[] { "AcademicYearId", "ActionType", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_AcademicYearId",
                table: "StudentAssessmentScores",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_AssessmentId",
                table: "StudentAssessmentScores",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_Course_Assessment",
                table: "StudentAssessmentScores",
                columns: new[] { "CourseId", "AssessmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_Hash",
                table: "StudentAssessmentScores",
                column: "ScoreHash");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_IsActive",
                table: "StudentAssessmentScores",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_ModifiedBy",
                table: "StudentAssessmentScores",
                column: "ModifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_RecordedBy",
                table: "StudentAssessmentScores",
                column: "RecordedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAssessmentScores_Student_Course_Year",
                table: "StudentAssessmentScores",
                columns: new[] { "StudentId", "CourseId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "UX_StudentAssessmentScores_Unique",
                table: "StudentAssessmentScores",
                columns: new[] { "StudentId", "CourseId", "AssessmentId", "AcademicYearId", "Semester" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseResults_CalculatedBy",
                table: "StudentCourseResults",
                column: "CalculatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseResults_CourseId",
                table: "StudentCourseResults",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseResults_Grade",
                table: "StudentCourseResults",
                column: "GradeLetter");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseResults_Hash",
                table: "StudentCourseResults",
                column: "ResultHash");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseResults_IsPassed",
                table: "StudentCourseResults",
                column: "IsPassed");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseResults_PublishedBy",
                table: "StudentCourseResults",
                column: "PublishedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseResults_Status",
                table: "StudentCourseResults",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseResults_Student_Course_Year",
                table: "StudentCourseResults",
                columns: new[] { "StudentId", "CourseId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseResults_Year_Status",
                table: "StudentCourseResults",
                columns: new[] { "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_StudentCourseResults_Unique",
                table: "StudentCourseResults",
                columns: new[] { "StudentId", "CourseId", "AcademicYearId", "Semester", "AttemptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResultAuditLogs");

            migrationBuilder.DropTable(
                name: "StudentAssessmentScores");

            migrationBuilder.DropTable(
                name: "StudentCourseResults");
        }
    }
}
