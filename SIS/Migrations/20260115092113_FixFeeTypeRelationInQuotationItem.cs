using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class FixFeeTypeRelationInQuotationItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuotationItems_OtherFees_FeeTypeId",
                table: "QuotationItems");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationItems_FeeTypes_FeeTypeId",
                table: "QuotationItems",
                column: "FeeTypeId",
                principalTable: "FeeTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuotationItems_FeeTypes_FeeTypeId",
                table: "QuotationItems");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationItems_OtherFees_FeeTypeId",
                table: "QuotationItems",
                column: "FeeTypeId",
                principalTable: "OtherFees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
