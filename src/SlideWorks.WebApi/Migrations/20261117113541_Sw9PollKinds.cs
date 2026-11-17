using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw9PollKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Polls",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_PollRounds_SessionId_Open",
                table: "PollRounds");

            migrationBuilder.DropIndex(
                name: "IX_PollRounds_SlideId_SessionId_Number",
                table: "PollRounds");

            migrationBuilder.AlterColumn<Guid>(
                name: "SlideId",
                table: "Polls",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "Polls",
                type: "TEXT",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Polls",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "SlideId",
                table: "PollRounds",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "PollRounds",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PollId",
                table: "PollRounds",
                type: "TEXT",
                nullable: false,
                defaultValue: Guid.Empty);

            // Backfill (SW-9 Q2): every existing poll and round is a slide's. An existing poll's new Id is its SlideId, which
            // is unique and already in the format EF writes, and each round points at it. This runs before the indexes below
            // and the table rebuilds that make Id the key. The rebuilt PollRounds is filled while the old Polls still
            // stands, and SQLite only accepts a foreign key to a unique column, so the old table gets a unique index on Id
            // first; it goes when the rebuild drops that table.
            migrationBuilder.Sql("""UPDATE "Polls" SET "Id" = "SlideId", "Kind" = 'Slide';""");
            migrationBuilder.Sql("""UPDATE "PollRounds" SET "PollId" = "SlideId", "Kind" = 'Slide';""");
            migrationBuilder.Sql("""CREATE UNIQUE INDEX "IX_Polls_Id_Backfill" ON "Polls" ("Id");""");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Polls",
                table: "Polls",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "PollOptions",
                columns: table => new
                {
                    RoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollOptions", x => new { x.RoundId, x.Position });
                    table.CheckConstraint("CK_PollOptions_Text_Length", "length(\"Text\") <= 200");
                    table.ForeignKey(
                        name: "FK_PollOptions_PollRounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "PollRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Polls_DeckId_Qa",
                table: "Polls",
                column: "DeckId",
                unique: true,
                filter: "\"Kind\" = 'Qa'");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_SlideId",
                table: "Polls",
                column: "SlideId",
                unique: true,
                filter: "\"SlideId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Polls_Kind_SlideId",
                table: "Polls",
                sql: "(\"Kind\" = 'Slide' AND \"SlideId\" IS NOT NULL) OR (\"Kind\" = 'Qa' AND \"SlideId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_PollRounds_PollId_SessionId_Number",
                table: "PollRounds",
                columns: new[] { "PollId", "SessionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollRounds_SessionId_Kind_Open",
                table: "PollRounds",
                columns: new[] { "SessionId", "Kind" },
                unique: true,
                filter: "\"ClosedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PollRounds_SlideId",
                table: "PollRounds",
                column: "SlideId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PollRounds_Kind_SlideId",
                table: "PollRounds",
                sql: "(\"Kind\" = 'Slide' AND \"SlideId\" IS NOT NULL) OR (\"Kind\" = 'Qa' AND \"SlideId\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_PollRounds_Polls_PollId",
                table: "PollRounds",
                column: "PollId",
                principalTable: "Polls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PollRounds_Polls_PollId",
                table: "PollRounds");

            migrationBuilder.DropTable(
                name: "PollOptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Polls",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_Polls_DeckId_Qa",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_Polls_SlideId",
                table: "Polls");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Polls_Kind_SlideId",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_PollRounds_PollId_SessionId_Number",
                table: "PollRounds");

            migrationBuilder.DropIndex(
                name: "IX_PollRounds_SessionId_Kind_Open",
                table: "PollRounds");

            migrationBuilder.DropIndex(
                name: "IX_PollRounds_SlideId",
                table: "PollRounds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PollRounds_Kind_SlideId",
                table: "PollRounds");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "PollRounds");

            migrationBuilder.DropColumn(
                name: "PollId",
                table: "PollRounds");

            migrationBuilder.AlterColumn<Guid>(
                name: "SlideId",
                table: "Polls",
                type: "TEXT",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SlideId",
                table: "PollRounds",
                type: "TEXT",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Polls",
                table: "Polls",
                column: "SlideId");

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
        }
    }
}
