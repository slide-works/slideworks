using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw9Qa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QaSettingsRows",
                columns: table => new
                {
                    DeckId = table.Column<string>(type: "TEXT", nullable: false),
                    Questions = table.Column<string>(type: "TEXT", nullable: false),
                    Review = table.Column<bool>(type: "INTEGER", nullable: false),
                    Names = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reactions = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowOn = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QaSettingsRows", x => x.DeckId);
                    table.CheckConstraint("CK_QaSettingsRows_Questions", "\"Questions\" IN ('Open', 'Closed', 'Off')");
                    table.CheckConstraint("CK_QaSettingsRows_ShowOn", "\"ShowOn\" IN ('AnySlide', 'QaSlide')");
                    table.ForeignKey(
                        name: "FK_QaSettingsRows_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QaSlideSwitchRows",
                columns: table => new
                {
                    SlideId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Questions = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reactions = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QaSlideSwitchRows", x => x.SlideId);
                    table.ForeignKey(
                        name: "FK_QaSlideSwitchRows_Slides_SlideId",
                        column: x => x.SlideId,
                        principalTable: "Slides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    RoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    GuestId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    HiddenAt = table.Column<long>(type: "INTEGER", nullable: true),
                    OnScreen = table.Column<bool>(type: "INTEGER", nullable: false),
                    AskedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    AnsweredAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => new { x.RoundId, x.Position });
                    table.CheckConstraint("CK_Questions_Name_Length", "\"Name\" IS NULL OR length(\"Name\") <= 30");
                    table.CheckConstraint("CK_Questions_OnScreen", "\"OnScreen\" = 0 OR (\"State\" = 'Open' AND \"HiddenAt\" IS NULL)");
                    table.CheckConstraint("CK_Questions_State", "\"State\" IN ('Waiting', 'Open', 'Answered')");
                    table.ForeignKey(
                        name: "FK_Questions_PollOptions_RoundId_Position",
                        columns: x => new { x.RoundId, x.Position },
                        principalTable: "PollOptions",
                        principalColumns: new[] { "RoundId", "Position" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReactionGuests",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionGuests", x => new { x.SessionId, x.GuestId });
                    table.ForeignKey(
                        name: "FK_ReactionGuests_AudienceSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AudienceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReactionTotals",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SlideId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionTotals", x => new { x.SessionId, x.SlideId, x.Emoji });
                    table.CheckConstraint("CK_ReactionTotals_Count", "\"Count\" >= 0");
                    table.CheckConstraint("CK_ReactionTotals_Emoji", "\"Emoji\" IN ('ThumbsUp', 'Heart', 'Laugh', 'Wow', 'Clap')");
                    table.ForeignKey(
                        name: "FK_ReactionTotals_AudienceSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AudienceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReactionTotals_Slides_SlideId",
                        column: x => x.SlideId,
                        principalTable: "Slides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_RoundId_OnScreen",
                table: "Questions",
                column: "RoundId",
                unique: true,
                filter: "\"OnScreen\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ReactionTotals_SlideId",
                table: "ReactionTotals",
                column: "SlideId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QaSettingsRows");

            migrationBuilder.DropTable(
                name: "QaSlideSwitchRows");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "ReactionGuests");

            migrationBuilder.DropTable(
                name: "ReactionTotals");
        }
    }
}
