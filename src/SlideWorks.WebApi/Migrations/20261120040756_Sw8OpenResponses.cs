using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw8OpenResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpenResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckId = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    SlideId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenResponses", x => x.Id);
                    table.CheckConstraint("CK_OpenResponses_Kind", "\"Kind\" IN ('WordCloud', 'OpenText')");
                    table.ForeignKey(
                        name: "FK_OpenResponses_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpenRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OpenResponseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ClosedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RevealedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenRounds", x => x.Id);
                    table.CheckConstraint("CK_OpenRounds_Number", "\"Number\" >= 1");
                    table.CheckConstraint("CK_OpenRounds_Revision", "\"Revision\" >= 0");
                    table.ForeignKey(
                        name: "FK_OpenRounds_AudienceSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AudienceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpenRounds_OpenResponses_OpenResponseId",
                        column: x => x.OpenResponseId,
                        principalTable: "OpenResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpenEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TextKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HiddenAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenEntries", x => x.Id);
                    table.CheckConstraint("CK_OpenEntries_Position", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_OpenEntries_OpenRounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "OpenRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpenEntries_RoundId_GuestId_Position",
                table: "OpenEntries",
                columns: new[] { "RoundId", "GuestId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenEntries_RoundId_TextKey",
                table: "OpenEntries",
                columns: new[] { "RoundId", "TextKey" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenResponses_DeckId",
                table: "OpenResponses",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenResponses_SlideId",
                table: "OpenResponses",
                column: "SlideId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenRounds_OpenResponseId_SessionId_Number",
                table: "OpenRounds",
                columns: new[] { "OpenResponseId", "SessionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenRounds_SessionId",
                table: "OpenRounds",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpenEntries");

            migrationBuilder.DropTable(
                name: "OpenRounds");

            migrationBuilder.DropTable(
                name: "OpenResponses");
        }
    }
}
