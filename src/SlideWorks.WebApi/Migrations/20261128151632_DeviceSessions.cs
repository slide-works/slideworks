using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class DeviceSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SecretHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    EndedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    EndReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSessions", x => x.Id);
                    table.CheckConstraint("CK_DeviceSessions_DeviceName_Length", "length(\"DeviceName\") <= 80");
                    table.CheckConstraint("CK_DeviceSessions_EndReason", "\"EndReason\" IS NULL OR \"EndReason\" IN ('AppSignOut', 'WebSignOut', 'Expired', 'AccessChanged')");
                    table.ForeignKey(
                        name: "FK_DeviceSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceSignInCodes",
                columns: table => new
                {
                    CodeHash = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Challenge = table.Column<string>(type: "TEXT", nullable: false),
                    AccessStamp = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UsedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSignInCodes", x => x.CodeHash);
                    table.CheckConstraint("CK_DeviceSignInCodes_DeviceName_Length", "length(\"DeviceName\") <= 80");
                    table.ForeignKey(
                        name: "FK_DeviceSignInCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSessions_SecretHash",
                table: "DeviceSessions",
                column: "SecretHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSessions_UserId",
                table: "DeviceSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSignInCodes_UserId",
                table: "DeviceSignInCodes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceSessions");

            migrationBuilder.DropTable(
                name: "DeviceSignInCodes");
        }
    }
}
