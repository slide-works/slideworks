using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw5AudienceJoin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JoinCode",
                table: "Decks",
                type: "TEXT",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            // Backfills a distinct code for every existing deck before the unique index goes on. 387911 shares no factor
            // with 900000, so stepping the row id by it, from a random start, visits distinct codes in 100000-999999.
            var start = System.Security.Cryptography.RandomNumberGenerator.GetInt32(900000);
            migrationBuilder.Sql($"UPDATE Decks SET JoinCode = CAST(100000 + ((rowid * 387911 + {start}) % 900000) AS TEXT);");

            migrationBuilder.CreateTable(
                name: "AudienceSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckId = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    EndedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    PeakJoined = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinSlideFirstShownAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudienceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudienceSessions_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudienceJoins",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<string>(type: "TEXT", nullable: false),
                    JoinedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudienceJoins", x => new { x.SessionId, x.GuestId });
                    table.ForeignKey(
                        name: "FK_AudienceJoins_AudienceSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AudienceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Decks_JoinCode",
                table: "Decks",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudienceSessions_DeckId_StartedAt",
                table: "AudienceSessions",
                columns: new[] { "DeckId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudienceJoins");

            migrationBuilder.DropTable(
                name: "AudienceSessions");

            migrationBuilder.DropIndex(
                name: "IX_Decks_JoinCode",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "JoinCode",
                table: "Decks");
        }
    }
}
