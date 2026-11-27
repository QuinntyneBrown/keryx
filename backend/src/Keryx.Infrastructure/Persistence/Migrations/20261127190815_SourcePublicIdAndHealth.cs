using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SourcePublicIdAndHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "Sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Health",
                table: "Sources",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "healthy");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastFetchedAt",
                table: "Sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSucceededAt",
                table: "Sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Sources",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            // Sources seeded before public ids existed each draw one: 12 characters of an md5,
            // with the hex digits 0, 1, 8 and 9 mapped to w, x, y and z so every character is in
            // the base32 alphabet that PublicIds draws from (ADR 0007).
            migrationBuilder.Sql(
                """UPDATE "Sources" SET "PublicId" = 'src_' || translate(substr(md5(random()::text || "Id"::text), 1, 12), '0189', 'wxyz') WHERE "PublicId" = '';""");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_PublicId",
                table: "Sources",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sources_PublicId",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "Health",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "LastFetchedAt",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "LastSucceededAt",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Sources");
        }
    }
}
