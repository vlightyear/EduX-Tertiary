using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SIS.Migrations
{
    /// <inheritdoc />
    public partial class AddGeopliticalModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Nations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Provinces_Nations_NationId",
                        column: x => x.NationId,
                        principalTable: "Nations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProvinceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Districts_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Constituencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DistrictId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Constituencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Constituencies_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Wards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConstituencyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wards_Constituencies_ConstituencyId",
                        column: x => x.ConstituencyId,
                        principalTable: "Constituencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Nations",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[] { 1, "ZM", "Zambia" });

            migrationBuilder.InsertData(
                table: "Provinces",
                columns: new[] { "Id", "Name", "NationId" },
                values: new object[,]
                {
                    { 1, "Central", 1 },
                    { 2, "Copperbelt", 1 },
                    { 3, "Eastern", 1 },
                    { 4, "Luapula", 1 },
                    { 5, "Lusaka", 1 },
                    { 6, "Muchinga", 1 },
                    { 7, "Northern", 1 },
                    { 8, "North-Western", 1 },
                    { 9, "Southern", 1 },
                    { 10, "Western", 1 }
                });

            migrationBuilder.InsertData(
                table: "Districts",
                columns: new[] { "Id", "Name", "ProvinceId" },
                values: new object[,]
                {
                    { 1, "Chibombo", 1 },
                    { 2, "Chisamba", 1 },
                    { 3, "Chitambo", 1 },
                    { 4, "Kabwe", 1 },
                    { 5, "Kapiri Mposhi", 1 },
                    { 6, "Luano", 1 },
                    { 7, "Mkushi", 1 },
                    { 8, "Mumbwa", 1 },
                    { 9, "Ngabwe", 1 },
                    { 10, "Serenje", 1 },
                    { 11, "Shibuyunji", 1 },
                    { 12, "Chililabombwe", 2 },
                    { 13, "Chingola", 2 },
                    { 14, "Kalulushi", 2 },
                    { 15, "Kitwe", 2 },
                    { 16, "Luanshya", 2 },
                    { 17, "Lufwanyama", 2 },
                    { 18, "Masaiti", 2 },
                    { 19, "Mpongwe", 2 },
                    { 20, "Mufulira", 2 },
                    { 21, "Ndola", 2 },
                    { 22, "Chadiza", 3 },
                    { 23, "Chasefu", 3 },
                    { 24, "Chipangali", 3 },
                    { 25, "Chipata", 3 },
                    { 26, "Katete", 3 },
                    { 27, "Kasenengwa", 3 },
                    { 28, "Lundazi", 3 },
                    { 29, "Lusangazi", 3 },
                    { 30, "Lumezi", 3 },
                    { 31, "Mambwe", 3 },
                    { 32, "Nyimba", 3 },
                    { 33, "Petauke", 3 },
                    { 34, "Sinda", 3 },
                    { 35, "Vubwi", 3 },
                    { 36, "Mkaika", 3 },
                    { 37, "Chembe", 4 },
                    { 38, "Chiengi", 4 },
                    { 39, "Chifunabuli", 4 },
                    { 40, "Chipili", 4 },
                    { 41, "Kawambwa", 4 },
                    { 42, "Lunga", 4 },
                    { 43, "Mansa", 4 },
                    { 44, "Milenge", 4 },
                    { 45, "Mwansabombwe", 4 },
                    { 46, "Mwense", 4 },
                    { 47, "Nchelenge", 4 },
                    { 48, "Samfya", 4 },
                    { 49, "Chilanga", 5 },
                    { 50, "Chongwe", 5 },
                    { 51, "Kafue", 5 },
                    { 52, "Luangwa", 5 },
                    { 53, "Lusaka", 5 },
                    { 54, "Rufunsa", 5 }
                });

            migrationBuilder.InsertData(
                table: "Constituencies",
                columns: new[] { "Id", "DistrictId", "Name" },
                values: new object[,]
                {
                    { 1, 4, "Bwacha" },
                    { 2, 2, "Chisamba" },
                    { 3, 3, "Chitambo" },
                    { 4, 4, "Kabwe Central" },
                    { 5, 5, "Kapiri Mposhi" },
                    { 6, 1, "Katuba" },
                    { 7, 1, "Keembe" },
                    { 8, 7, "Mkushi North" },
                    { 9, 7, "Mkushi South" },
                    { 10, 8, "Mumbwa" },
                    { 11, 10, "Serenje" },
                    { 12, 21, "Bwana Mkubwa" },
                    { 13, 21, "Chifubu" },
                    { 14, 12, "Chililabombwe" },
                    { 15, 15, "Chimwemwe" },
                    { 16, 13, "Chingola" },
                    { 17, 15, "Kabushi" },
                    { 18, 14, "Kalulushi" },
                    { 19, 15, "Kwacha" },
                    { 20, 16, "Luanshya" },
                    { 21, 20, "Mufulira" },
                    { 22, 21, "Ndola Central" },
                    { 23, 15, "Nkana" },
                    { 24, 16, "Roan" },
                    { 25, 15, "Wusakile" },
                    { 26, 53, "Chawama" },
                    { 27, 49, "Chilanga" },
                    { 28, 50, "Chongwe" },
                    { 29, 53, "Kabwata" },
                    { 30, 51, "Kafue" },
                    { 31, 53, "Kanyama" },
                    { 32, 53, "Lusaka Central" },
                    { 33, 53, "Mandevu" },
                    { 34, 53, "Matero" },
                    { 35, 53, "Munali" },
                    { 36, 54, "Rufunsa" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Constituencies_DistrictId",
                table: "Constituencies",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Districts_ProvinceId",
                table: "Districts",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_NationId",
                table: "Provinces",
                column: "NationId");

            migrationBuilder.CreateIndex(
                name: "IX_Wards_ConstituencyId",
                table: "Wards",
                column: "ConstituencyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Wards");

            migrationBuilder.DropTable(
                name: "Constituencies");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropTable(
                name: "Provinces");

            migrationBuilder.DropTable(
                name: "Nations");
        }
    }
}
