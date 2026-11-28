using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class SlideTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TranslationsJson",
                table: "Slides",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeckTranslationSettings",
                columns: table => new
                {
                    DeckId = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Primary = table.Column<string>(type: "TEXT", nullable: false),
                    LanguagesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckTranslationSettings", x => x.DeckId);
                    table.ForeignKey(
                        name: "FK_DeckTranslationSettings_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeckTranslationSettings");

            migrationBuilder.DropColumn(
                name: "TranslationsJson",
                table: "Slides");
        }
    }
}
