using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw50Templates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Surface",
                table: "Slides",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemplateVersion",
                table: "Decks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Access = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                    table.CheckConstraint("CK_Templates_Access", "\"Access\" IN ('Private', 'Link', 'Named')");
                    table.CheckConstraint("CK_Templates_Name_Length", "length(\"Name\") <= 60");
                    table.ForeignKey(
                        name: "FK_Templates_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateAssets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TemplateId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    Bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    MetaJson = table.Column<string>(type: "TEXT", nullable: true),
                    RightsConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateAssets", x => new { x.TemplateId, x.Id });
                    table.CheckConstraint("CK_TemplateAssets_FileName_Length", "length(\"FileName\") <= 120");
                    table.CheckConstraint("CK_TemplateAssets_Kind", "\"Kind\" IN ('font', 'logo', 'art', 'layout')");
                    table.ForeignKey(
                        name: "FK_TemplateAssets_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateUploads",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "TEXT", nullable: false),
                    TemplateId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    RightsConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UsedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateUploads", x => x.TokenHash);
                    table.CheckConstraint("CK_TemplateUploads_FileName_Length", "length(\"FileName\") <= 120");
                    table.CheckConstraint("CK_TemplateUploads_Kind", "\"Kind\" IN ('font', 'logo', 'art', 'layout')");
                    table.ForeignKey(
                        name: "FK_TemplateUploads_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateVersions",
                columns: table => new
                {
                    TemplateId = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    ManifestJson = table.Column<string>(type: "TEXT", nullable: false),
                    FitWidthsJson = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateVersions", x => new { x.TemplateId, x.Version });
                    table.CheckConstraint("CK_TemplateVersions_Version", "\"Version\" >= 0");
                    table.ForeignKey(
                        name: "FK_TemplateVersions_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateViewers",
                columns: table => new
                {
                    TemplateId = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateViewers", x => new { x.TemplateId, x.Email });
                    table.ForeignKey(
                        name: "FK_TemplateViewers_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Templates_OwnerId",
                table: "Templates",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateUploads_TemplateId",
                table: "TemplateUploads",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateAssets");

            migrationBuilder.DropTable(
                name: "TemplateUploads");

            migrationBuilder.DropTable(
                name: "TemplateVersions");

            migrationBuilder.DropTable(
                name: "TemplateViewers");

            migrationBuilder.DropTable(
                name: "Templates");

            migrationBuilder.DropColumn(
                name: "Surface",
                table: "Slides");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "Decks");
        }
    }
}
