using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LookUp.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddHexIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Messages_Hex",
                table: "Messages",
                column: "Hex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_Hex",
                table: "Messages");
        }
    }
}
