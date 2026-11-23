using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw672OpenEntryFirstSent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Arrival",
                table: "OpenEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            // Backfill: rows already stored are numbered 1, 2, ... per round in their current rowid order, which is the wall
            // order today, so no wall number changes. Controls used to order open-text answers by guest id, so a control number
            // can change to match the wall. A word guest's rows that were replaced sort by their last send.
            migrationBuilder.Sql("""
                UPDATE "OpenEntries" SET "Arrival" = (
                    SELECT COUNT(*) FROM "OpenEntries" AS o WHERE o."RoundId" = "OpenEntries"."RoundId" AND o.rowid <= "OpenEntries".rowid)
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OpenEntries_RoundId_Arrival",
                table: "OpenEntries",
                columns: new[] { "RoundId", "Arrival" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OpenEntries_RoundId_Arrival",
                table: "OpenEntries");

            migrationBuilder.DropColumn(
                name: "Arrival",
                table: "OpenEntries");
        }
    }
}
