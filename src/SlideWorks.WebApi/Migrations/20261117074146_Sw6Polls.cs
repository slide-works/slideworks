using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw6Polls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PollRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SlideId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ClosedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RevealedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    AnswerShownAt = table.Column<long>(type: "INTEGER", nullable: true),
                    JoinedAtClose = table.Column<int>(type: "INTEGER", nullable: true),
                    Final = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollRounds_AudienceSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AudienceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PollRounds_Slides_SlideId",
                        column: x => x.SlideId,
                        principalTable: "Slides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Polls",
                columns: table => new
                {
                    SlideId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckId = table.Column<string>(type: "TEXT", nullable: false),
                    Pick = table.Column<string>(type: "TEXT", nullable: false),
                    Max = table.Column<int>(type: "INTEGER", nullable: true),
                    Results = table.Column<string>(type: "TEXT", nullable: false),
                    Correct = table.Column<int>(type: "INTEGER", nullable: true),
                    Opens = table.Column<string>(type: "TEXT", nullable: false),
                    Privacy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Polls", x => x.SlideId);
                    table.ForeignKey(
                        name: "FK_Polls_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Polls_Slides_SlideId",
                        column: x => x.SlideId,
                        principalTable: "Slides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PollAnswers",
                columns: table => new
                {
                    RoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<string>(type: "TEXT", nullable: false),
                    Picks = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollAnswers", x => new { x.RoundId, x.GuestId });
                    table.ForeignKey(
                        name: "FK_PollAnswers_PollRounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "PollRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PollRounds_SessionId_Open",
                table: "PollRounds",
                column: "SessionId",
                unique: true,
                filter: "\"ClosedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PollRounds_SlideId_SessionId_Number",
                table: "PollRounds",
                columns: new[] { "SlideId", "SessionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Polls_DeckId",
                table: "Polls",
                column: "DeckId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PollAnswers");

            migrationBuilder.DropTable(
                name: "Polls");

            migrationBuilder.DropTable(
                name: "PollRounds");
        }
    }
}
