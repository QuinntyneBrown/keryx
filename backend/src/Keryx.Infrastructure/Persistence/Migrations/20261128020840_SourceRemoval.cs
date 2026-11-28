using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SourceRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sources_Kind_NormalizedAddress",
                table: "Sources");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovedAt",
                table: "Sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Kind_NormalizedAddress",
                table: "Sources",
                columns: new[] { "Kind", "NormalizedAddress" },
                unique: true,
                filter: "\"RemovedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sources_Kind_NormalizedAddress",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "Sources");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Kind_NormalizedAddress",
                table: "Sources",
                columns: new[] { "Kind", "NormalizedAddress" },
                unique: true);
        }
    }
}
