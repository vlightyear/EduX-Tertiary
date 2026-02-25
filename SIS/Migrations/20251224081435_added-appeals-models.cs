using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class addedappealsmodels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppealTypeConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Fee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiresFee = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealTypeConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultAppeals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Semester = table.Column<int>(type: "int", nullable: false),
                    AppealType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SupportingDocuments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppealFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FeePaid = table.Column<bool>(type: "bit", nullable: false),
                    FeePaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OriginalMark = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    RevisedMark = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    OriginalGrade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RevisedGrade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Response = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResponseBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ResponseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalDecision = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecisionBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DecisionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsEscalated = table.Column<bool>(type: "bit", nullable: false),
                    EscalationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EscalatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultAppeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultAppeals_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultAppeals_AspNetUsers_DecisionBy",
                        column: x => x.DecisionBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultAppeals_AspNetUsers_ResponseBy",
                        column: x => x.ResponseBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultAppeals_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultAppeals_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppealId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealStatusHistories_AspNetUsers_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppealStatusHistories_ResultAppeals_AppealId",
                        column: x => x.AppealId,
                        principalTable: "ResultAppeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppealTypeConfigs",
                columns: new[] { "Id", "Description", "DisplayOrder", "Fee", "IsActive", "RequiresFee", "TypeCode", "TypeName" },
                values: new object[,]
                {
                    { 1, "Request for complete re-marking of examination script by a different examiner", 1, 500.00m, true, true, "Remark", "Remark (Re-marking of script)" },
                    { 2, "Request to review the marking for any errors or omissions", 2, 0m, true, false, "Review", "Review (Review of marking)" },
                    { 3, "Request to verify the totaling and calculation of marks", 3, 0m, true, false, "Recalculation", "Recalculation (Check totals)" },
                    { 4, "Appeal for marks that were not recorded or are missing from the system", 4, 0m, true, false, "MissingMarks", "Missing Marks" },
                    { 5, "Dispute regarding the final grade assigned", 5, 0m, true, false, "GradeDispute", "Grade Dispute" },
                    { 6, "Other appeal types not covered by the above categories", 6, 0m, true, false, "Other", "Other" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppealStatusHistories_AppealId",
                table: "AppealStatusHistories",
                column: "AppealId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealStatusHistories_ChangedAt",
                table: "AppealStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppealStatusHistories_ChangedBy",
                table: "AppealStatusHistories",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AppealTypeConfigs_IsActive",
                table: "AppealTypeConfigs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AppealTypeConfigs_TypeCode",
                table: "AppealTypeConfigs",
                column: "TypeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultAppeals_AppealType",
                table: "ResultAppeals",
                column: "AppealType");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAppeals_CourseId",
                table: "ResultAppeals",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAppeals_DecisionBy",
                table: "ResultAppeals",
                column: "DecisionBy");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAppeals_ResponseBy",
                table: "ResultAppeals",
                column: "ResponseBy");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAppeals_Status",
                table: "ResultAppeals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAppeals_Student_Course_Year_Semester",
                table: "ResultAppeals",
                columns: new[] { "StudentId", "CourseId", "AcademicYearId", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultAppeals_StudentId",
                table: "ResultAppeals",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultAppeals_Type_FeePaid",
                table: "ResultAppeals",
                columns: new[] { "AppealType", "FeePaid" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultAppeals_Year_Status",
                table: "ResultAppeals",
                columns: new[] { "AcademicYearId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealStatusHistories");

            migrationBuilder.DropTable(
                name: "AppealTypeConfigs");

            migrationBuilder.DropTable(
                name: "ResultAppeals");
        }
    }
}
