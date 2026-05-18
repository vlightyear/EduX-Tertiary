using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class ExtendAppUserAndSchoolWithAdministrativeHierachies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConstituencyId",
                table: "Schools",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "Schools",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NationId",
                table: "Schools",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProvinceId",
                table: "Schools",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WardId",
                table: "Schools",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConstituencyId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NationId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProvinceId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WardId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schools_ConstituencyId",
                table: "Schools",
                column: "ConstituencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_DistrictId",
                table: "Schools",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_NationId",
                table: "Schools",
                column: "NationId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_ProvinceId",
                table: "Schools",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_WardId",
                table: "Schools",
                column: "WardId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ConstituencyId",
                table: "AspNetUsers",
                column: "ConstituencyId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DistrictId",
                table: "AspNetUsers",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NationId",
                table: "AspNetUsers",
                column: "NationId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ProvinceId",
                table: "AspNetUsers",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SchoolId",
                table: "AspNetUsers",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_WardId",
                table: "AspNetUsers",
                column: "WardId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Constituencies_ConstituencyId",
                table: "AspNetUsers",
                column: "ConstituencyId",
                principalTable: "Constituencies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Districts_DistrictId",
                table: "AspNetUsers",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Nations_NationId",
                table: "AspNetUsers",
                column: "NationId",
                principalTable: "Nations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Provinces_ProvinceId",
                table: "AspNetUsers",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Schools_SchoolId",
                table: "AspNetUsers",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Wards_WardId",
                table: "AspNetUsers",
                column: "WardId",
                principalTable: "Wards",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_Constituencies_ConstituencyId",
                table: "Schools",
                column: "ConstituencyId",
                principalTable: "Constituencies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_Districts_DistrictId",
                table: "Schools",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_Nations_NationId",
                table: "Schools",
                column: "NationId",
                principalTable: "Nations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_Provinces_ProvinceId",
                table: "Schools",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_Wards_WardId",
                table: "Schools",
                column: "WardId",
                principalTable: "Wards",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Constituencies_ConstituencyId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Districts_DistrictId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Nations_NationId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Provinces_ProvinceId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Schools_SchoolId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Wards_WardId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Schools_Constituencies_ConstituencyId",
                table: "Schools");

            migrationBuilder.DropForeignKey(
                name: "FK_Schools_Districts_DistrictId",
                table: "Schools");

            migrationBuilder.DropForeignKey(
                name: "FK_Schools_Nations_NationId",
                table: "Schools");

            migrationBuilder.DropForeignKey(
                name: "FK_Schools_Provinces_ProvinceId",
                table: "Schools");

            migrationBuilder.DropForeignKey(
                name: "FK_Schools_Wards_WardId",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_Schools_ConstituencyId",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_Schools_DistrictId",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_Schools_NationId",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_Schools_ProvinceId",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_Schools_WardId",
                table: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ConstituencyId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DistrictId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NationId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ProvinceId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SchoolId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_WardId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ConstituencyId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "NationId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "ProvinceId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "WardId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "ConstituencyId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NationId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProvinceId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WardId",
                table: "AspNetUsers");
        }
    }
}
