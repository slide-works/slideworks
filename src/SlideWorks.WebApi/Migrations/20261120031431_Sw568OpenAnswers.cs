using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw568OpenAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Answers",
                table: "Polls",
                type: "TEXT",
                nullable: false,
                defaultValue: "Choice");

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "PollAnswers",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PollAnswers_Text_Length",
                table: "PollAnswers",
                sql: "\"Text\" IS NULL OR length(\"Text\") <= 200");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PollAnswers_Text_Length",
                table: "PollAnswers");

            migrationBuilder.DropColumn(
                name: "Answers",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "PollAnswers");
        }
    }
}
