using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class correctedAcacdemicYearIdtoAcademicYearIdofgradeconfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AcacdemicYearId",
                table: "GradeConfigurations",
                newName: "AcademicYearId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AcademicYearId",
                table: "GradeConfigurations",
                newName: "AcacdemicYearId");
        }
    }
}
