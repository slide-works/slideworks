using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class ProfileAndSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarColour",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NameSetByPerson",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Access",
                table: "DeckViewers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "IssuedAt",
                table: "AgentTokens",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastUsedAt",
                table: "AgentTokens",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailSuppressions",
                columns: table => new
                {
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSuppressions", x => x.Email);
                });

            migrationBuilder.CreateTable(
                name: "ShareEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientEmail = table.Column<string>(type: "TEXT", nullable: false),
                    SharerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckId = table.Column<string>(type: "TEXT", nullable: false),
                    SentAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareEmails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShareRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SharerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckId = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    AddedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareRecipients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShareEmails_RecipientEmail_SentAt",
                table: "ShareEmails",
                columns: new[] { "RecipientEmail", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ShareRecipients_SharerId_AddedAt",
                table: "ShareRecipients",
                columns: new[] { "SharerId", "AddedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailSuppressions");

            migrationBuilder.DropTable(
                name: "ShareEmails");

            migrationBuilder.DropTable(
                name: "ShareRecipients");

            migrationBuilder.DropColumn(
                name: "AvatarColour",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NameSetByPerson",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhotoId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Access",
                table: "DeckViewers");

            migrationBuilder.DropColumn(
                name: "IssuedAt",
                table: "AgentTokens");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "AgentTokens");
        }
    }
}
