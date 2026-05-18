using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LookUp.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddTrigramIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Messages_BlockHash",
                table: "Messages",
                column: "BlockHash");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_BlockMinedAt",
                table: "Messages",
                column: "BlockMinedAt");

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(@"CREATE INDEX ix_messages_message_trgm ON ""Messages"" USING GIN (""Message"" gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_BlockHash",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_BlockMinedAt",
                table: "Messages");

            // Note: don't drop the pg_trgm extension here — other things may depend on it
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_messages_message_trgm;");
        }
    }
}
