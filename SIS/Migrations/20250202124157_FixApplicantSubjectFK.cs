using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class FixApplicantSubjectFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applicants_Schools_SchoolId",
                table: "Applicants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApplicantSubjects",
                table: "ApplicantSubjects");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "ApplicantSubjects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApplicantSubjects",
                table: "ApplicantSubjects",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantSubjects_ApplicantId",
                table: "ApplicantSubjects",
                column: "ApplicantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Applicants_Schools_SchoolId",
                table: "Applicants",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applicants_Schools_SchoolId",
                table: "Applicants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApplicantSubjects",
                table: "ApplicantSubjects");

            migrationBuilder.DropIndex(
                name: "IX_ApplicantSubjects_ApplicantId",
                table: "ApplicantSubjects");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "ApplicantSubjects");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApplicantSubjects",
                table: "ApplicantSubjects",
                columns: new[] { "ApplicantId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Applicants_Schools_SchoolId",
                table: "Applicants",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
