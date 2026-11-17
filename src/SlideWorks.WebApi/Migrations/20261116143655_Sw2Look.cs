using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw2Look : Migration
    {
        // Existing decks become the talk template with the cobalt palette (L5), not a draft, with no logo and no chrome
        // overrides. Existing slides are not deep dives.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeepDive",
                table: "Slides",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ChromeJson",
                table: "Decks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Draft",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LogoImageId",
                table: "Decks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Palette",
                table: "Decks",
                type: "TEXT",
                nullable: false,
                defaultValue: "cobalt");

            migrationBuilder.AddColumn<string>(
                name: "Template",
                table: "Decks",
                type: "TEXT",
                nullable: false,
                defaultValue: "talk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeepDive",
                table: "Slides");

            migrationBuilder.DropColumn(
                name: "ChromeJson",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "Draft",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "LogoImageId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "Palette",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "Template",
                table: "Decks");
        }
    }
}
