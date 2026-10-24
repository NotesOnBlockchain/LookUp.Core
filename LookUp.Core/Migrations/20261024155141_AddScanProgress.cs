using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LookUp.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddScanProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScanProgress",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false),
                    NextBlockHeight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanProgress", x => x.ID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScanProgress");
        }
    }
}
