using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LookUp.Core.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBlockIndexColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockIndex",
                table: "Messages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BlockIndex",
                table: "Messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
