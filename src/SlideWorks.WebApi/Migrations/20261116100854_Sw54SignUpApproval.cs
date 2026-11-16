using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlideWorks.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Sw54SignUpApproval : Migration
    {
        // Existing users become Pending and not admin (D9): the SLIDEWORKS_ADMIN_EMAIL bootstrap promotes the admin on their
        // next sign-in.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Access",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<long>(
                name: "AccessChangedAt",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccessChangedBy",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Access",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccessChangedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccessChangedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");
        }
    }
}
