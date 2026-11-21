using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw8OpenResponseSlideKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_OpenEntries_Text_Length",
                table: "OpenEntries",
                sql: "length(\"Text\") <= 200");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OpenEntries_TextKey_Length",
                table: "OpenEntries",
                sql: "length(\"TextKey\") <= 200");

            migrationBuilder.AddForeignKey(
                name: "FK_OpenResponses_Slides_SlideId",
                table: "OpenResponses",
                column: "SlideId",
                principalTable: "Slides",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpenResponses_Slides_SlideId",
                table: "OpenResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OpenEntries_Text_Length",
                table: "OpenEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OpenEntries_TextKey_Length",
                table: "OpenEntries");
        }
    }
}
